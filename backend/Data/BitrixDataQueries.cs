using Npgsql;

namespace InformesAvanzar.Api.Data;

public static class BitrixDataQueries
{
    public static async Task<IResult> GetSyncSummaryAsync(
        string pipeline,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH selected_pipelines AS (
                SELECT id, slug, name
                FROM bitrix.pipelines
                WHERE @pipeline = 'all' OR slug = @pipeline
            ),
            deal_counts AS (
                SELECT count(d.id)::integer AS deals_count
                FROM bitrix.deals d
                JOIN selected_pipelines p ON p.id = d.pipeline_id
            ),
            stage_counts AS (
                SELECT count(s.id)::integer AS stages_count
                FROM bitrix.pipeline_stages s
                JOIN selected_pipelines p ON p.id = s.pipeline_id
            ),
            user_counts AS (
                SELECT count(id)::integer AS users_count
                FROM bitrix.users
            ),
            last_run AS (
                SELECT sr.entity_type, sr.status, sr.records_read, sr.records_written, sr.error_message, sr.created_at, sr.finished_at
                FROM bitrix.sync_runs sr
                WHERE (
                    @pipeline = 'all'
                    AND sr.entity_type IN ('user', 'deal_category_stage')
                )
                OR sr.entity_type = ('deal:' || @pipeline)
                ORDER BY sr.created_at DESC
                LIMIT 1
            )
            SELECT
                (SELECT deals_count FROM deal_counts),
                (SELECT stages_count FROM stage_counts),
                (SELECT users_count FROM user_counts),
                lr.entity_type,
                lr.status,
                lr.records_read,
                lr.records_written,
                lr.error_message,
                lr.created_at,
                lr.finished_at
            FROM last_run lr
            RIGHT JOIN deal_counts ON true
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("pipeline", pipeline);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.Ok(new
            {
                dealsCount = 0,
                stagesCount = 0,
                usersCount = 0,
                lastSync = (object?)null
            });
        }

        return Results.Ok(new
        {
            dealsCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            stagesCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            usersCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            lastSync = reader.IsDBNull(3)
                ? null
                : new
                {
                    entityType = reader.GetString(3),
                    status = reader.GetString(4),
                    recordsRead = reader.GetInt32(5),
                    recordsWritten = reader.GetInt32(6),
                    errorMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                    createdAt = reader.GetDateTime(8),
                    finishedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9)
                }
        });
    }

    public static async Task<IResult> GetDealsAsync(
        string pipeline,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.bitrix_id,
                d.title,
                p.name AS pipeline,
                d.stage_id,
                s.name AS stage_name,
                u.full_name AS responsible_name,
                d.opportunity,
                d.currency_id,
                d.bitrix_updated_at
            FROM bitrix.deals d
            JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            LEFT JOIN bitrix.pipeline_stages s ON s.pipeline_id = p.id AND s.bitrix_stage_id = d.stage_id
            LEFT JOIN bitrix.users u ON u.connection_id = d.connection_id AND u.bitrix_id = d.assigned_by_bitrix_id
            WHERE (@pipeline = 'all' OR p.slug = @pipeline)
            ORDER BY d.bitrix_updated_at DESC NULLS LAST, d.bitrix_id DESC
            LIMIT 100;
            """;

        var rows = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("pipeline", pipeline);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new
            {
                bitrixId = reader.GetString(0),
                title = reader.GetString(1),
                pipeline = reader.GetString(2),
                stageId = reader.IsDBNull(3) ? null : reader.GetString(3),
                stageName = reader.IsDBNull(4) ? null : reader.GetString(4),
                responsibleName = reader.IsDBNull(5) ? null : reader.GetString(5),
                opportunity = reader.IsDBNull(6) ? (decimal?)null : reader.GetDecimal(6),
                currencyId = reader.IsDBNull(7) ? null : reader.GetString(7),
                updatedAt = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8)
            });
        }

        return Results.Ok(rows);
    }

    public static async Task<IResult> GetUsersAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT bitrix_id, full_name, email, department, active
            FROM bitrix.users
            ORDER BY full_name
            LIMIT 100;
            """;

        var rows = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new
            {
                bitrixId = reader.GetString(0),
                fullName = reader.GetString(1),
                email = reader.IsDBNull(2) ? null : reader.GetString(2),
                department = reader.IsDBNull(3) ? null : reader.GetString(3),
                active = reader.GetBoolean(4)
            });
        }

        return Results.Ok(rows);
    }

    public static async Task<IResult> GetStagesAsync(
        string pipeline,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.name AS pipeline, s.bitrix_stage_id, s.name, s.sort_order, s.status_type
            FROM bitrix.pipeline_stages s
            JOIN bitrix.pipelines p ON p.id = s.pipeline_id
            WHERE (@pipeline = 'all' OR p.slug = @pipeline)
            ORDER BY p.sync_order, s.sort_order NULLS LAST, s.name
            LIMIT 200;
            """;

        var rows = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("pipeline", pipeline);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new
            {
                pipeline = reader.GetString(0),
                stageId = reader.GetString(1),
                name = reader.GetString(2),
                sortOrder = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                statusType = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return Results.Ok(rows);
    }
}
