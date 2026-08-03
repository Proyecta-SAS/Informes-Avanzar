using Npgsql;

namespace InformesAvanzar.Api.Data;

public interface IDatabaseHealthCheck
{
    Task CheckAsync(CancellationToken cancellationToken);
}

public sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IDatabaseHealthCheck
{
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
