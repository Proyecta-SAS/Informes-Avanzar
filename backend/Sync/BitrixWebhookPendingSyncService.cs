using Npgsql;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixWebhookPendingSyncService(
    IBitrixSyncRepository repository,
    IBitrixDealSyncService dealSyncService,
    NpgsqlDataSource dataSource) : IBitrixWebhookPendingSyncService
{
    public async Task<SyncResult> ProcessPendingDealChangesAsync(int limit, CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 5000);
        var connectionInfo = await repository.GetActiveConnectionAsync(cancellationToken);
        var syncRunId = await repository.StartRunAsync(connectionInfo.Id, "deal:webhook-pending", SyncMode.Incremental, cancellationToken);
        var recordsRead = 0;
        var recordsWritten = 0;

        try
        {
            var events = await TakePendingEventsAsync(effectiveLimit, cancellationToken);
            foreach (var pendingEvent in events)
            {
                recordsRead++;
                var result = await dealSyncService.SyncDealByIdAsync(pendingEvent.BitrixId, syncRunId, cancellationToken);
                if (string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    recordsWritten += result.RecordsWritten;
                    await MarkProcessedAsync(pendingEvent.Id, syncRunId, cancellationToken);
                }
                else
                {
                    await MarkFailedAsync(pendingEvent.Id, result.ErrorMessage ?? "No se pudo sincronizar la negociacion.", cancellationToken);
                }

                await repository.UpdateRunProgressAsync(syncRunId, recordsRead, recordsWritten, cancellationToken);
            }

            await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, CancellationToken.None);
            return new SyncResult(syncRunId, "deal:webhook-pending", "incremental", "succeeded", recordsRead, recordsWritten);
        }
        catch (Exception ex)
        {
            await repository.FinishRunAsync(syncRunId, "failed", recordsRead, recordsWritten, ex.Message, CancellationToken.None);
            return new SyncResult(syncRunId, "deal:webhook-pending", "incremental", "failed", recordsRead, recordsWritten, ex.Message);
        }
    }

    private async Task<IReadOnlyList<PendingDealEvent>> TakePendingEventsAsync(int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            WITH selected AS (
                SELECT id
                FROM bitrix.outgoing_webhook_events
                WHERE entity_type = 'deal'
                  AND status IN ('pending', 'failed')
                  AND attempts < 5
                ORDER BY last_seen_at
                LIMIT @limit
                FOR UPDATE SKIP LOCKED
            )
            UPDATE bitrix.outgoing_webhook_events event
            SET status = 'processing',
                attempts = attempts + 1,
                last_error = NULL
            FROM selected
            WHERE event.id = selected.id
            RETURNING event.id, event.bitrix_id;
            """;

        var events = new List<PendingDealEvent>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("limit", limit);
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new PendingDealEvent(reader.GetGuid(0), reader.GetString(1)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return events;
    }

    private async Task MarkProcessedAsync(Guid eventId, Guid syncRunId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE bitrix.outgoing_webhook_events
            SET status = 'processed',
                processed_at = now(),
                sync_run_id = @syncRunId,
                last_error = NULL
            WHERE id = @eventId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        command.Parameters.AddWithValue("syncRunId", syncRunId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(Guid eventId, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE bitrix.outgoing_webhook_events
            SET status = 'failed',
                last_error = @errorMessage
            WHERE id = @eventId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        command.Parameters.AddWithValue("errorMessage", errorMessage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record PendingDealEvent(Guid Id, string BitrixId);
}
