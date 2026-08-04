using Npgsql;

namespace InformesAvanzar.Api.Reports;

public sealed class ReportAccessService(NpgsqlDataSource dataSource) : IReportAccessService
{
    public async Task<bool> UserCanAccessReportAsync(
        Guid userId,
        string reportCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM reporting.report_definitions rd
                WHERE rd.code = @reportCode
                  AND rd.status = 'published'
                  AND rd.deleted_at IS NULL
                  AND (
                      rd.owner_user_id = @userId
                      OR EXISTS (
                          SELECT 1 FROM reporting.report_access ra
                          WHERE ra.report_definition_id = rd.id
                            AND ra.user_id = @userId
                            AND (ra.expires_at IS NULL OR ra.expires_at > now())
                      )
                      OR EXISTS (
                          SELECT 1 FROM reporting.report_access ra
                          JOIN auth.user_roles ur ON ur.role_id = ra.role_id
                          WHERE ra.report_definition_id = rd.id
                            AND ur.user_id = @userId
                            AND (ra.expires_at IS NULL OR ra.expires_at > now())
                      )
                  )
            );
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("reportCode", reportCode);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task<string[]> GetAccessibleReportCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT rd.code
            FROM reporting.report_definitions rd
            WHERE rd.status = 'published' AND rd.deleted_at IS NULL
              AND (
                rd.owner_user_id = @userId
                OR EXISTS (
                    SELECT 1 FROM reporting.report_access ra
                    WHERE ra.report_definition_id = rd.id AND ra.user_id = @userId
                      AND (ra.expires_at IS NULL OR ra.expires_at > now())
                )
                OR EXISTS (
                    SELECT 1 FROM reporting.report_access ra
                    JOIN auth.user_roles ur ON ur.role_id = ra.role_id
                    WHERE ra.report_definition_id = rd.id AND ur.user_id = @userId
                      AND (ra.expires_at IS NULL OR ra.expires_at > now())
                )
              )
            ORDER BY rd.code;
            """;
        var result = new List<string>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    public async Task<bool> UserCanAccessReportAsync(
        Guid userId,
        Guid reportDefinitionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM reporting.report_definitions rd
                WHERE rd.id = @reportDefinitionId
                  AND rd.status = 'published'
                  AND rd.deleted_at IS NULL
                  AND (
                      rd.owner_user_id = @userId
                      OR EXISTS (
                          SELECT 1
                          FROM reporting.report_access ra
                          WHERE ra.report_definition_id = rd.id
                            AND ra.user_id = @userId
                            AND (ra.expires_at IS NULL OR ra.expires_at > now())
                      )
                      OR EXISTS (
                          SELECT 1
                          FROM reporting.report_access ra
                          JOIN auth.user_roles ur ON ur.role_id = ra.role_id
                          WHERE ra.report_definition_id = rd.id
                            AND ur.user_id = @userId
                            AND (ra.expires_at IS NULL OR ra.expires_at > now())
                      )
                  )
            );
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("reportDefinitionId", reportDefinitionId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }
}
