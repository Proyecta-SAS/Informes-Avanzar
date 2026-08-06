using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InformesAvanzar.Api.Bitrix;
using Npgsql;
using NpgsqlTypes;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixActivitySyncService(
    IBitrixClient bitrixClient,
    IBitrixSyncRepository repository,
    NpgsqlDataSource dataSource) : IBitrixActivitySyncService
{
    private static readonly string[] ActivityFields =
    [
        "ID",
        "OWNER_ID",
        "OWNER_TYPE_ID",
        "TYPE_ID",
        "SUBJECT",
        "RESPONSIBLE_ID",
        "COMPLETED",
        "CREATED",
        "START_TIME",
        "DEADLINE",
        "DATE_CREATE",
        "LAST_UPDATED",
        "END_TIME",
        "COMMUNICATIONS",
        "SETTINGS"
    ];

    public async Task<SyncResult> SyncActivitiesAsync(CancellationToken cancellationToken)
    {
        var connectionInfo = await repository.GetActiveConnectionAsync(cancellationToken);
        var syncRunId = await repository.StartRunAsync(connectionInfo.Id, "activity", SyncMode.Full, cancellationToken);

        var recordsRead = 0;
        var recordsWritten = 0;

        try
        {
            int? start = 0;
            var visitedStarts = new HashSet<int>();
            await using var dbConnection = await dataSource.OpenConnectionAsync(cancellationToken);

            while (start is not null)
            {
                if (!visitedStarts.Add(start.Value))
                {
                    break;
                }

                using var response = await bitrixClient.CallAsync(
                    BitrixMethod.ActivityList,
                    BuildActivityListParameters(start.Value),
                    cancellationToken);

                var root = response.RootElement;

                if (root.TryGetProperty("error", out var error))
                {
                    throw new InvalidOperationException(error.GetString());
                }

                if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                if (result.GetArrayLength() == 0)
                {
                    break;
                }

                await using (var transaction = await dbConnection.BeginTransactionAsync(cancellationToken))
                {
                    foreach (var activity in result.EnumerateArray())
                    {
                        recordsRead++;
                        if (await UpsertActivityAsync(
                            dbConnection,
                            transaction,
                            connectionInfo.Id,
                            syncRunId,
                            activity,
                            cancellationToken))
                        {
                            recordsWritten++;
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);
                }

                await repository.UpdateRunProgressAsync(syncRunId, recordsRead, recordsWritten, cancellationToken);
                start = TryGetNext(root);
            }

            await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, cancellationToken);
            return new SyncResult(syncRunId, "activity", "full", "succeeded", recordsRead, recordsWritten);
        }
        catch (Exception ex)
        {
            await repository.FinishRunAsync(syncRunId, "failed", recordsRead, recordsWritten, ex.Message, cancellationToken);
            return new SyncResult(syncRunId, "activity", "full", "failed", recordsRead, recordsWritten, ex.Message);
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildActivityListParameters(int start)
    {
        yield return new KeyValuePair<string, string>("order[ID]", "ASC");
        yield return new KeyValuePair<string, string>("filter[OWNER_TYPE_ID]", "2");
        foreach (var responsibleId in new[] { "941", "9482", "16230", "2070", "17844", "18384", "17890" })
        {
            yield return new KeyValuePair<string, string>("filter[RESPONSIBLE_ID][]", responsibleId);
        }
        yield return new KeyValuePair<string, string>("filter[TYPE_ID][]", "6");
        yield return new KeyValuePair<string, string>("filter[TYPE_ID][]", "2");
        yield return new KeyValuePair<string, string>("filter[>=CREATED]", "2025-01-01T00:00:00+00:00");
        yield return new KeyValuePair<string, string>("filter[<CREATED]", "2026-01-01T00:00:00+00:00");
        yield return new KeyValuePair<string, string>("start", start.ToString());

        foreach (var field in ActivityFields)
        {
            yield return new KeyValuePair<string, string>("select[]", field);
        }
    }

    private static async Task<bool> UpsertActivityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        Guid syncRunId,
        JsonElement activity,
        CancellationToken cancellationToken)
    {
        var bitrixId = GetString(activity, "ID")
            ?? throw new InvalidOperationException("Bitrix activity without ID.");
        var payload = JsonSerializer.Serialize(activity);
        var hash = Sha256(payload);

        if (await ActivityPayloadIsUnchangedAsync(connection, transaction, connectionId, bitrixId, hash, cancellationToken))
        {
            return false;
        }

        var rawPayloadId = await InsertRawPayloadAsync(
            connection,
            transaction,
            connectionId,
            syncRunId,
            bitrixId,
            payload,
            hash,
            cancellationToken);

        const string sql = """
            INSERT INTO bitrix.activities (
                connection_id,
                bitrix_id,
                owner_type,
                owner_bitrix_id,
                type_id,
                subject,
                responsible_bitrix_id,
                completed,
                deadline_at,
                raw_payload_id,
                bitrix_created_at,
                bitrix_updated_at
            )
            VALUES (
                @connectionId,
                @bitrixId,
                @ownerType,
                @ownerBitrixId,
                @typeId,
                @subject,
                @responsibleBitrixId,
                @completed,
                @deadlineAt,
                @rawPayloadId,
                @bitrixCreatedAt,
                @bitrixUpdatedAt
            )
            ON CONFLICT (connection_id, bitrix_id) DO UPDATE
            SET owner_type = EXCLUDED.owner_type,
                owner_bitrix_id = EXCLUDED.owner_bitrix_id,
                type_id = EXCLUDED.type_id,
                subject = EXCLUDED.subject,
                responsible_bitrix_id = EXCLUDED.responsible_bitrix_id,
                completed = EXCLUDED.completed,
                deadline_at = EXCLUDED.deadline_at,
                raw_payload_id = EXCLUDED.raw_payload_id,
                bitrix_created_at = EXCLUDED.bitrix_created_at,
                bitrix_updated_at = EXCLUDED.bitrix_updated_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("bitrixId", bitrixId);
        command.Parameters.AddWithValue("ownerType", (object?)GetString(activity, "OWNER_TYPE_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("ownerBitrixId", (object?)GetString(activity, "OWNER_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("typeId", (object?)GetString(activity, "TYPE_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("subject", (object?)GetString(activity, "SUBJECT") ?? DBNull.Value);
        command.Parameters.AddWithValue("responsibleBitrixId", (object?)GetString(activity, "RESPONSIBLE_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("completed", GetString(activity, "COMPLETED") == "Y");
        command.Parameters.AddWithValue("deadlineAt", (object?)GetDateTimeOffset(activity, "DEADLINE") ?? DBNull.Value);
        var bitrixCreatedAt = GetDateTimeOffset(activity, "CREATED")
            ?? GetDateTimeOffset(activity, "DATE_CREATE")
            ?? GetDateTimeOffset(activity, "START_TIME");
        command.Parameters.AddWithValue("rawPayloadId", rawPayloadId);
        command.Parameters.AddWithValue("bitrixCreatedAt", (object?)bitrixCreatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("bitrixUpdatedAt", (object?)GetDateTimeOffset(activity, "LAST_UPDATED") ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> ActivityPayloadIsUnchangedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        string bitrixId,
        string hash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT rp.payload_hash
            FROM bitrix.activities a
            JOIN bitrix.raw_payloads rp ON rp.id = a.raw_payload_id
            WHERE a.connection_id = @connectionId
              AND a.bitrix_id = @bitrixId
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("bitrixId", bitrixId);

        var currentHash = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return string.Equals(currentHash, hash, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> InsertRawPayloadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        Guid syncRunId,
        string bitrixId,
        string payload,
        string hash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO bitrix.raw_payloads (
                connection_id,
                sync_run_id,
                entity_type,
                bitrix_id,
                payload,
                payload_hash
            )
            VALUES (
                @connectionId,
                @syncRunId,
                'activity',
                @bitrixId,
                @payload,
                @payloadHash
            )
            ON CONFLICT (connection_id, entity_type, bitrix_id, payload_hash) DO UPDATE
            SET received_at = bitrix.raw_payloads.received_at
            RETURNING id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("syncRunId", syncRunId);
        command.Parameters.AddWithValue("bitrixId", bitrixId);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = payload;
        command.Parameters.AddWithValue("payloadHash", hash);

        return (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Could not insert raw activity payload."));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static int? TryGetNext(JsonElement root)
    {
        if (!root.TryGetProperty("next", out var next))
        {
            return null;
        }

        if (next.ValueKind == JsonValueKind.Number && next.TryGetInt32(out var nextNumber))
        {
            return nextNumber;
        }

        if (next.ValueKind == JsonValueKind.String && int.TryParse(next.GetString(), out var nextStringNumber))
        {
            return nextStringNumber;
        }

        return null;
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
