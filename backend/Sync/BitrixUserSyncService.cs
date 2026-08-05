using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InformesAvanzar.Api.Bitrix;
using InformesAvanzar.Api.Data;
using Npgsql;
using NpgsqlTypes;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixUserSyncService(
    IBitrixClient bitrixClient,
    IBitrixSyncRepository repository,
    NpgsqlDataSource dataSource) : IBitrixUserSyncService
{
    public async Task<SyncResult> SyncUsersAsync(CancellationToken cancellationToken)
    {
        var connectionInfo = await repository.GetActiveConnectionAsync(cancellationToken);
        var syncRunId = await repository.StartRunAsync(connectionInfo.Id, "user", SyncMode.Full, cancellationToken);

        var recordsRead = 0;
        var recordsWritten = 0;

        try
        {
            int? start = 0;
            var visitedStarts = new HashSet<int>();

            while (start is not null)
            {
                if (!visitedStarts.Add(start.Value))
                {
                    break;
                }

                using var response = await bitrixClient.CallAsync(
                    BitrixMethod.UserGet,
                    new[]
                    {
                        new KeyValuePair<string, string>("start", start.Value.ToString())
                    },
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

                foreach (var user in result.EnumerateArray())
                {
                    recordsRead++;
                    await UpsertUserAsync(connectionInfo.Id, syncRunId, user, cancellationToken);
                    recordsWritten++;
                }

                start = TryGetNext(root);
            }

            await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, CancellationToken.None);
            return new SyncResult(syncRunId, "user", "full", "succeeded", recordsRead, recordsWritten);
        }
        catch (Exception ex)
        {
            await repository.FinishRunAsync(syncRunId, "failed", recordsRead, recordsWritten, ex.Message, CancellationToken.None);
            return new SyncResult(syncRunId, "user", "full", "failed", recordsRead, recordsWritten, ex.Message);
        }
    }

    private async Task UpsertUserAsync(
        Guid connectionId,
        Guid syncRunId,
        JsonElement user,
        CancellationToken cancellationToken)
    {
        var bitrixId = GetString(user, "ID")
            ?? throw new InvalidOperationException("Bitrix user without ID.");
        var payload = JsonSerializer.Serialize(user);
        var hash = Sha256(payload);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var rawPayloadId = await InsertRawPayloadAsync(
            connection,
            transaction,
            connectionId,
            syncRunId,
            bitrixId,
            payload,
            hash,
            cancellationToken);

        const string upsertUserSql = """
            INSERT INTO bitrix.users (
                connection_id,
                bitrix_id,
                email,
                full_name,
                department,
                active,
                raw_payload_id,
                bitrix_updated_at
            )
            VALUES (
                @connectionId,
                @bitrixId,
                @email,
                @fullName,
                @department,
                @active,
                @rawPayloadId,
                @bitrixUpdatedAt
            )
            ON CONFLICT (connection_id, bitrix_id) DO UPDATE
            SET email = EXCLUDED.email,
                full_name = EXCLUDED.full_name,
                department = EXCLUDED.department,
                active = EXCLUDED.active,
                raw_payload_id = EXCLUDED.raw_payload_id,
                bitrix_updated_at = EXCLUDED.bitrix_updated_at;
            """;

        await using var command = new NpgsqlCommand(upsertUserSql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("bitrixId", bitrixId);
        var email = KnownBitrixCorrections.ResolveUserEmail(bitrixId, GetString(user, "EMAIL"));
        command.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
        command.Parameters.AddWithValue("fullName", BuildFullName(user));
        command.Parameters.AddWithValue("department", (object?)GetString(user, "WORK_DEPARTMENT") ?? DBNull.Value);
        command.Parameters.AddWithValue("active", GetString(user, "ACTIVE") != "N");
        command.Parameters.AddWithValue("rawPayloadId", rawPayloadId);
        command.Parameters.AddWithValue("bitrixUpdatedAt", DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
                'user',
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
            ?? throw new InvalidOperationException("Could not insert raw user payload."));
    }

    private static string BuildFullName(JsonElement user)
    {
        var parts = new[]
        {
            GetString(user, "NAME"),
            GetString(user, "LAST_NAME"),
            GetString(user, "SECOND_NAME")
        }.Where(part => !string.IsNullOrWhiteSpace(part));

        var fullName = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(fullName) ? $"Usuario {GetString(user, "ID")}" : fullName;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
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
