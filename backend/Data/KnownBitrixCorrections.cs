using Npgsql;

namespace InformesAvanzar.Api.Data;

public static class KnownBitrixCorrections
{
    private static readonly IReadOnlyDictionary<string, string> UserEmails =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["18434"] = "ma.galeano@avanzarsoluciones.com"
        };

    public static string? ResolveUserEmail(string bitrixId, string? bitrixEmail)
    {
        return UserEmails.TryGetValue(bitrixId, out var correctedEmail)
            ? correctedEmail
            : bitrixEmail;
    }

    public static async Task ApplyAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE bitrix.users
            SET email = 'ma.galeano@avanzarsoluciones.com',
                updated_at = now()
            WHERE bitrix_id = '18434'
              AND email IS DISTINCT FROM 'ma.galeano@avanzarsoluciones.com';

            UPDATE bitrix.departments
            SET head_bitrix_id = '18434',
                updated_at = now()
            WHERE id = 1324
              AND head_bitrix_id IS DISTINCT FROM '18434';
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
