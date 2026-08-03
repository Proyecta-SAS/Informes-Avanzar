using System.Globalization;
using Npgsql;

namespace InformesAvanzar.Api.Data;

public static class BitrixDataQueries
{
    public static async Task<object> GetDiegoRadicatedValuesAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH radicated AS (
                SELECT
                    CASE
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '22560' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ENERO%' THEN '01 ENE'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '22562' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%FEBRERO%' THEN '02 FEB'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39144' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MARZO%' THEN '03 MAR'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39146' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ABRIL%' THEN '04 ABR'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39148' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MAYO%' THEN '05 MAY'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39150' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JUNIO%' THEN '06 JUN'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39152' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JULIO%' THEN '07 JUL'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39154' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%AGOSTO%' THEN '08 AGO'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39156' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%SEPTIEMBRE%' THEN '09 SEP'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39158' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%OCTUBRE%' THEN '10 OCT'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39160' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%NOVIEMBRE%' THEN '11 NOV'
                        WHEN s.custom_fields ->> 'UF_CRM_1676419915' = '39162' OR UPPER(COALESCE(s.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%DICIEMBRE%' THEN '12 DIC'
                    END AS month,
                    COALESCE(NULLIF(u.full_name, ''), d.assigned_by_bitrix_id, 'Sin asesor') AS advisor,
                    d.opportunity AS amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots s
                    ON s.connection_id = d.connection_id
                    AND s.entity_type = 'deal'
                    AND s.bitrix_id = d.bitrix_id
                    AND s.is_deleted = false
                LEFT JOIN bitrix.users u
                    ON u.connection_id = d.connection_id
                    AND u.bitrix_id = d.assigned_by_bitrix_id
                    AND u.active = true
                WHERE
                    (
                        p.category_id IN (10, 28)
                        OR UPPER(p.name) IN ('1116 OPERATIVA', 'LP OPERATIVA', 'LP OPERATIVA 2445')
                    )
                    AND (
                        s.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR s.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
            )
            SELECT month, advisor, COALESCE(SUM(amount), 0) AS total_achieved
            FROM radicated
            WHERE month IS NOT NULL
            GROUP BY month, advisor
            ORDER BY total_achieved DESC, month, advisor
            LIMIT 1000;
            """;

        var items = new List<object>();
        decimal total = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year.ToString(CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var amount = reader.GetDecimal(2);
            total += amount;
            items.Add(new
            {
                month = reader.GetString(0),
                advisor = reader.GetString(1),
                totalAchieved = amount
            });
        }

        return new { year, totalAchieved = total, items };
    }

    public static async Task<object> GetDiegoCommercialDashboardAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string advisorSql = """
            SELECT
                COALESCE(NULLIF(u.full_name, ''), d.assigned_by_bitrix_id, 'Sin asesor') AS advisor,
                COUNT(*) AS negotiations,
                COUNT(*) FILTER (WHERE p.category_id IN (8, 26)) AS commercial_cases,
                COUNT(*) FILTER (WHERE p.category_id IN (10, 28)) AS radicated_cases,
                COALESCE(SUM(d.opportunity), 0) AS total_value
            FROM bitrix.deals d
            JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
            LEFT JOIN bitrix.users u
                ON u.connection_id = d.connection_id
                AND u.bitrix_id = d.assigned_by_bitrix_id
            WHERE
                (
                    p.category_id IN (8, 26)
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737654190' = @yearText
                        OR snapshot.custom_fields ->> 'UF_CRM_1737654190' = CASE @yearText
                            WHEN '2025' THEN '37058'
                            WHEN '2026' THEN '37060'
                            WHEN '2027' THEN '37062'
                        END
                    )
                )
                OR (
                    p.category_id IN (10, 28)
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @yearText
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @yearText
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                )
            GROUP BY 1
            ORDER BY negotiations DESC, advisor
            LIMIT 1000;
            """;

        const string stageSql = """
            SELECT
                p.slug,
                COALESCE(s.name, d.stage_id, 'Sin etapa') AS stage,
                COUNT(*) AS cases,
                COALESCE(SUM(d.opportunity), 0) AS total_value,
                COALESCE(s.sort_order, 9999) AS sort_order
            FROM bitrix.deals d
            JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            LEFT JOIN bitrix.pipeline_stages s
                ON s.pipeline_id = p.id
                AND s.bitrix_stage_id = d.stage_id
            GROUP BY p.slug, COALESCE(s.name, d.stage_id, 'Sin etapa'), COALESCE(s.sort_order, 9999)
            ORDER BY p.slug, sort_order, stage;
            """;

        const string departmentSql = """
            SELECT
                COALESCE(NULLIF(u.department, ''), 'Sin departamento') AS department,
                COUNT(*) AS cases,
                COALESCE(SUM(d.opportunity), 0) AS total_value
            FROM bitrix.deals d
            JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            LEFT JOIN bitrix.users u
                ON u.connection_id = d.connection_id
                AND u.bitrix_id = d.assigned_by_bitrix_id
            WHERE p.category_id IN (10, 28)
            GROUP BY 1
            ORDER BY total_value DESC, department
            LIMIT 1000;
            """;

        var advisors = new List<object>();
        var stages = new List<object>();
        var departments = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(advisorSql, connection))
        {
            command.Parameters.AddWithValue("yearText", year.ToString(CultureInfo.InvariantCulture));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                advisors.Add(new
                {
                    advisor = reader.GetString(0),
                    negotiations = reader.GetInt64(1),
                    commercialCases = reader.GetInt64(2),
                    radicatedCases = reader.GetInt64(3),
                    totalValue = reader.GetDecimal(4)
                });
            }
        }

        await using (var command = new NpgsqlCommand(stageSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                stages.Add(new
                {
                    pipeline = reader.GetString(0),
                    stage = reader.GetString(1),
                    cases = reader.GetInt64(2),
                    totalValue = reader.GetDecimal(3),
                    sortOrder = reader.GetInt32(4)
                });
            }
        }

        await using (var command = new NpgsqlCommand(departmentSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                departments.Add(new
                {
                    department = reader.GetString(0),
                    cases = reader.GetInt64(1),
                    totalValue = reader.GetDecimal(2)
                });
            }
        }

        return new { year, advisors, stages, departments };
    }

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
