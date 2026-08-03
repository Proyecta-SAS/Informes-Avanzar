using InformesAvanzar.Api.Bitrix;
using Npgsql;

namespace InformesAvanzar.Api.Sync;

public interface IBitrixSyncRepository
{
    Task<BitrixConnection> GetActiveConnectionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BitrixPipeline>> ListActivePipelinesAsync(CancellationToken cancellationToken);
    Task<Guid> StartRunAsync(Guid connectionId, string entityType, SyncMode mode, CancellationToken cancellationToken);
    Task UpdateRunProgressAsync(Guid syncRunId, int recordsRead, int recordsWritten, CancellationToken cancellationToken);
    Task FinishRunAsync(Guid syncRunId, string status, int recordsRead, int recordsWritten, string? errorMessage, CancellationToken cancellationToken);
    Task<bool> TryAcquireGlobalLockAsync(string ownerId, TimeSpan ttl, CancellationToken cancellationToken);
    Task ReleaseGlobalLockAsync(string ownerId, CancellationToken cancellationToken);
}

public sealed class BitrixSyncRepository(NpgsqlDataSource dataSource) : IBitrixSyncRepository
{
    public async Task<BitrixConnection> GetActiveConnectionAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, base_url
            FROM bitrix.connections
            WHERE status = 'active'
            ORDER BY created_at
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("No active Bitrix connection exists.");
        }

        return new BitrixConnection(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2));
    }

    public async Task<IReadOnlyList<BitrixPipeline>> ListActivePipelinesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT slug, name, category_id, domain, sync_order
            FROM bitrix.pipelines
            WHERE is_active = true
            ORDER BY sync_order, category_id;
            """;

        var pipelines = new List<BitrixPipeline>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            pipelines.Add(new BitrixPipeline(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt32(4)));
        }

        return pipelines;
    }

    public async Task<Guid> StartRunAsync(Guid connectionId, string entityType, SyncMode mode, CancellationToken cancellationToken)
    {
        const string guardSql = """
            UPDATE bitrix.sync_runs
            SET status = 'failed',
                finished_at = now(),
                error_message = 'Sync interrumpida o sin cierre automatico.'
            WHERE status = 'running'
              AND created_at < now() - interval '6 hours';
            """;

        const string activeRunSql = """
            SELECT entity_type
            FROM bitrix.sync_runs
            WHERE status = 'running'
            ORDER BY created_at DESC
            LIMIT 1;
            """;

        const string insertSql = """
            INSERT INTO bitrix.sync_runs (connection_id, entity_type, mode, status, started_at)
            VALUES (@connectionId, @entityType, @mode, 'running', now())
            RETURNING id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var guardCommand = new NpgsqlCommand(guardSql, connection, transaction))
        {
            await guardCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var activeCommand = new NpgsqlCommand(activeRunSql, connection, transaction))
        {
            var activeEntity = (string?)await activeCommand.ExecuteScalarAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(activeEntity))
            {
                throw new InvalidOperationException($"Ya hay una sincronizacion activa: {activeEntity}.");
            }
        }

        await using var command = new NpgsqlCommand(insertSql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("entityType", entityType);
        command.Parameters.AddWithValue("mode", ToDbMode(mode));

        var syncRunId = (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Could not create sync run."));

        await transaction.CommitAsync(cancellationToken);
        return syncRunId;
    }

    public async Task UpdateRunProgressAsync(Guid syncRunId, int recordsRead, int recordsWritten, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE bitrix.sync_runs
            SET records_read = @recordsRead,
                records_written = @recordsWritten
            WHERE id = @syncRunId
              AND status = 'running';
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("syncRunId", syncRunId);
        command.Parameters.AddWithValue("recordsRead", recordsRead);
        command.Parameters.AddWithValue("recordsWritten", recordsWritten);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task FinishRunAsync(Guid syncRunId, string status, int recordsRead, int recordsWritten, string? errorMessage, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE bitrix.sync_runs
            SET status = @status,
                finished_at = now(),
                records_read = @recordsRead,
                records_written = @recordsWritten,
                error_message = @errorMessage
            WHERE id = @syncRunId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("syncRunId", syncRunId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("recordsRead", recordsRead);
        command.Parameters.AddWithValue("recordsWritten", recordsWritten);
        command.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> TryAcquireGlobalLockAsync(string ownerId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO bitrix.sync_locks (lock_key, owner_id, expires_at)
            VALUES ('sync_global', @ownerId, now() + @ttl)
            ON CONFLICT (lock_key) DO UPDATE
            SET owner_id = EXCLUDED.owner_id,
                acquired_at = now(),
                expires_at = EXCLUDED.expires_at
            WHERE bitrix.sync_locks.expires_at < now()
            RETURNING lock_key;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ownerId", ownerId);
        command.Parameters.AddWithValue("ttl", ttl);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    public async Task ReleaseGlobalLockAsync(string ownerId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM bitrix.sync_locks WHERE lock_key = 'sync_global' AND owner_id = @ownerId;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ownerId", ownerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ToDbMode(SyncMode mode) => mode switch
    {
        SyncMode.Full => "full",
        SyncMode.Incremental => "incremental",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };
}
