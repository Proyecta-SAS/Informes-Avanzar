using System.Globalization;
using Npgsql;

namespace InformesAvanzar.Api.Data;

public static class BitrixDataQueries
{
    public static async Task<IResult> GetSyncStateAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT entity_type, mode, status, records_read, records_written, started_at, created_at
            FROM bitrix.sync_runs
            WHERE status = 'running'
            ORDER BY started_at DESC NULLS LAST, created_at DESC
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.Ok(new { isSyncing = false, activeRun = (object?)null });
        }

        return Results.Ok(new
        {
            isSyncing = true,
            activeRun = new
            {
                entityType = reader.GetString(0),
                mode = reader.GetString(1),
                status = reader.GetString(2),
                recordsRead = reader.GetInt32(3),
                recordsWritten = reader.GetInt32(4),
                startedAt = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                createdAt = reader.GetDateTime(6)
            }
        });
    }

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
            ORDER BY total_achieved DESC, month, advisor;
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
            WHERE p.category_id IN (8, 10, 26, 28)
            GROUP BY 1
            ORDER BY advisor;
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
            ORDER BY total_value DESC, department;
            """;

        const string possibleClosePnncSql = """
            WITH classified AS (
                SELECT
                    CASE
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) LIKE '%REVISION DE LIDER%' THEN '01 Revisión líder'
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) LIKE '%RADICACION POR VALIDAR%' THEN '02 Radicación por validar'
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) ~ '(DOCUMENTACION PENDIENTE COMERCIAL|DOCUMENTOS PENDIENTES)' THEN '03 Documentación pendiente'
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) ~ '(DOCUMENTACION SUBSANADA COMERCIAL|DOCUMENTOS SUBSANADOS|DOCUMENTOS SUBSANDADOS COMERCIAL)' THEN '04 Documentación subsanada'
                    END AS stage,
                    d.opportunity
                FROM bitrix.deals d
                JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                LEFT JOIN bitrix.pipeline_stages s ON s.pipeline_id = p.id AND s.bitrix_stage_id = d.stage_id
                JOIN LATERAL (
                    SELECT payload
                    FROM bitrix.raw_payloads
                    WHERE entity_type = 'user' AND bitrix_id = d.assigned_by_bitrix_id
                    ORDER BY received_at DESC
                    LIMIT 1
                ) raw_user ON true
                WHERE p.category_id IN (26, 28)
                  AND EXISTS (
                      SELECT 1
                      FROM jsonb_array_elements_text(raw_user.payload->'UF_DEPARTMENT') department_id
                      WHERE department_id = ANY(@insolvencyDepartmentIds)
                  )
            )
            SELECT stage, COALESCE(SUM(opportunity),0) AS amount, COUNT(*)::bigint AS cases
            FROM classified
            WHERE stage IS NOT NULL
            GROUP BY stage
            ORDER BY stage;
            """;

        var advisors = new List<object>();
        var stages = new List<object>();
        var departments = new List<object>();
        var possibleClosePnnc = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(advisorSql, connection))
        {
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

        await using (var command = new NpgsqlCommand(possibleClosePnncSql, connection))
        {
            command.Parameters.AddWithValue("insolvencyDepartmentIds", new[] { "1332", "1324", "1366", "1414", "1346", "1430", "1432", "1254", "1310", "1426", "1326", "1374", "1402", "1404", "1428", "1308", "1328", "1320", "1252", "1408", "1410", "1412" });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                possibleClosePnnc.Add(new { stage = reader.GetString(0), amount = reader.GetDecimal(1), cases = reader.GetInt64(2) });
        }

        return new { year, advisors, stages, departments, possibleClosePnnc };
    }

    public static async Task<object> GetDiegoPortfolioCollectionsAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH payment_fields(date_key, value_key) AS (
                VALUES
                    ('UF_CRM_1616543199911', 'UF_CRM_1616543235645'),
                    ('UF_CRM_1616543363164', 'UF_CRM_1616543387444'),
                    ('UF_CRM_1616543459676', 'UF_CRM_1616543489629'),
                    ('UF_CRM_1616543556711', 'UF_CRM_1616543576996'),
                    ('UF_CRM_1616543676428', 'UF_CRM_1616543703340'),
                    ('UF_CRM_1616543806805', 'UF_CRM_1616543829877'),
                    ('UF_CRM_1616543903340', 'UF_CRM_1616543924037'),
                    ('UF_CRM_1709396834305', 'UF_CRM_1709151333092'),
                    ('UF_CRM_1616544028572', 'UF_CRM_1616544047801'),
                    ('UF_CRM_1616544121180', 'UF_CRM_1616544143695'),
                    ('UF_CRM_1676486990987', 'UF_CRM_1676487293788'),
                    ('UF_CRM_1676487033939', 'UF_CRM_1676487304887')
            ), payments AS (
                SELECT
                    CASE EXTRACT(MONTH FROM (snapshot.custom_fields ->> fields.date_key)::date)::int
                        WHEN 1 THEN '01 ENE' WHEN 2 THEN '02 FEB' WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR' WHEN 5 THEN '05 MAY' WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL' WHEN 8 THEN '08 AGO' WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT' WHEN 11 THEN '11 NOV' WHEN 12 THEN '12 DIC'
                    END AS month,
                    CASE
                        WHEN p.category_id IN (12, 302) THEN 'LÍNEA RCH'
                        WHEN p.category_id IN (68, 308) THEN 'LÍNEA INSOLVENCIA'
                    END AS commercial_line,
                    CASE
                        WHEN snapshot.custom_fields ->> fields.value_key LIKE '%,%'
                        THEN REPLACE(REPLACE(REPLACE(snapshot.custom_fields ->> fields.value_key, '$', ''), '.', ''), ',', '.')::numeric
                        ELSE NULLIF(REGEXP_REPLACE(snapshot.custom_fields ->> fields.value_key, '[^0-9.-]', '', 'g'), '')::numeric
                    END AS amount
                FROM bitrix.deals d
                JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                CROSS JOIN payment_fields fields
                WHERE p.category_id IN (12, 68, 302, 308)
                    AND NULLIF(snapshot.custom_fields ->> fields.date_key, '') IS NOT NULL
                    AND NULLIF(snapshot.custom_fields ->> fields.value_key, '') IS NOT NULL
                    AND EXTRACT(YEAR FROM (snapshot.custom_fields ->> fields.date_key)::date) = @year
            )
            SELECT month, commercial_line, COALESCE(SUM(amount), 0) AS collected
            FROM payments
            WHERE month IS NOT NULL AND amount IS NOT NULL
            GROUP BY month, commercial_line
            ORDER BY month, commercial_line;
            """;

        var items = new List<object>();
        decimal total = 0;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var collected = reader.GetDecimal(2);
            total += collected;
            items.Add(new
            {
                month = reader.GetString(0),
                commercialLine = reader.GetString(1),
                collected
            });
        }

        await reader.DisposeAsync();
        const string portfolioSql = """
            WITH portfolio AS (
                SELECT
                    COALESCE(NULLIF(users.full_name, ''), deals.assigned_by_bitrix_id, 'Sin asesor') AS advisor,
                    CASE
                        WHEN pipelines.category_id IN (12, 302) THEN 'RCH'
                        WHEN pipelines.category_id IN (68, 308) THEN 'Insolvencia'
                    END AS commercial_line,
                    UPPER(TRANSLATE(COALESCE(stages.name, deals.stage_id, ''),
                        'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) AS stage_name,
                    COALESCE(deals.opportunity, 0) AS amount
                FROM bitrix.deals deals
                JOIN bitrix.pipelines pipelines ON pipelines.id = deals.pipeline_id
                LEFT JOIN bitrix.pipeline_stages stages
                    ON stages.pipeline_id = pipelines.id
                    AND stages.bitrix_stage_id = deals.stage_id
                LEFT JOIN bitrix.users users
                    ON users.connection_id = deals.connection_id
                    AND users.bitrix_id = deals.assigned_by_bitrix_id
                WHERE pipelines.category_id IN (12, 68, 302, 308)
            )
            SELECT
                advisor,
                commercial_line,
                COALESCE(SUM(amount) FILTER (WHERE stage_name !~ '(NOVEDAD|OBJEC|MORA|BAJA|ELIMINAR|GANADO|EXITOS|FACTUR|PAZ Y SALVO|PAGAD)'), 0) AS receivable,
                COALESCE(SUM(amount) FILTER (WHERE stage_name ~ '(NOVEDAD|OBJEC|MORA|BAJA|ELIMINAR)'), 0) AS with_novelty,
                COALESCE(SUM(amount) FILTER (WHERE stage_name ~ '(GANADO|EXITOS|FACTUR|PAZ Y SALVO|PAGAD)'), 0) AS successful
            FROM portfolio
            GROUP BY advisor, commercial_line
            HAVING SUM(amount) > 0
            ORDER BY receivable DESC, advisor;
            """;

        var portfolio = new List<object>();
        await using (var portfolioCommand = new NpgsqlCommand(portfolioSql, connection))
        await using (var portfolioReader = await portfolioCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await portfolioReader.ReadAsync(cancellationToken))
            {
                portfolio.Add(new
                {
                    advisor = portfolioReader.GetString(0),
                    commercialLine = portfolioReader.GetString(1),
                    receivable = portfolioReader.GetDecimal(2),
                    withNovelty = portfolioReader.GetDecimal(3),
                    successful = portfolioReader.GetDecimal(4)
                });
            }
        }

        return new { year, totalCollected = total, items, portfolio };
    }

    public static async Task<object> GetDiegoLeadershipAndCommissionsAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string leadershipSql = """
            WITH RECURSIVE user_departments AS (
                SELECT DISTINCT
                    u.connection_id,
                    u.bitrix_id,
                    u.full_name,
                    (jsonb_array_elements_text(payload.payload -> 'UF_DEPARTMENT'))::bigint AS department_id
                FROM bitrix.users u
                JOIN bitrix.raw_payloads payload ON payload.id = u.raw_payload_id
                WHERE u.active = true AND jsonb_typeof(payload.payload -> 'UF_DEPARTMENT') = 'array'
            ), hierarchy AS (
                SELECT
                    ud.connection_id, ud.bitrix_id, ud.full_name,
                    department.id, department.name, department.parent_id, 1 AS depth
                FROM user_departments ud
                JOIN bitrix.departments department ON department.id = ud.department_id
                UNION ALL
                SELECT
                    hierarchy.connection_id, hierarchy.bitrix_id, hierarchy.full_name,
                    parent.id, parent.name, parent.parent_id, hierarchy.depth + 1
                FROM hierarchy
                JOIN bitrix.departments parent ON parent.id = hierarchy.parent_id
                WHERE hierarchy.depth < 8
            ), people AS (
                SELECT
                    connection_id,
                    bitrix_id,
                    full_name,
                    MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. COOR%') AS coordinator,
                    MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. LIDER%') AS leader
                FROM hierarchy
                GROUP BY connection_id, bitrix_id, full_name
            ), radicated AS (
                SELECT
                    d.connection_id,
                    d.assigned_by_bitrix_id,
                    CASE
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22560' THEN '01 ENE'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22562' THEN '02 FEB'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39144' THEN '03 MAR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39146' THEN '04 ABR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39148' THEN '05 MAY'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39150' THEN '06 JUN'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39152' THEN '07 JUL'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39154' THEN '08 AGO'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39156' THEN '09 SEP'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39158' THEN '10 OCT'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39160' THEN '11 NOV'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39162' THEN '12 DIC'
                    END AS month,
                    COALESCE(d.opportunity, 0) AS amount
                FROM bitrix.deals d
                JOIN bitrix.pipelines pipeline ON pipeline.id = d.pipeline_id
                JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                WHERE pipeline.category_id IN (10, 28)
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @yearText
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @yearText
                            WHEN '2024' THEN '37206' WHEN '2025' THEN '37036' WHEN '2026' THEN '39138'
                        END
                    )
            )
            SELECT
                radicated.month,
                COALESCE(people.leader, 'Sin líder') AS leader,
                COALESCE(people.coordinator, 'Sin coordinador') AS coordinator,
                COALESCE(SUM(radicated.amount), 0) AS total_achieved
            FROM radicated
            LEFT JOIN people
                ON people.connection_id = radicated.connection_id
                AND people.bitrix_id = radicated.assigned_by_bitrix_id
            WHERE radicated.month IS NOT NULL
            GROUP BY radicated.month, people.leader, people.coordinator
            ORDER BY total_achieved DESC;
            """;

        const string commissionsSql = """
            SELECT
                TO_CHAR(COALESCE(d.bitrix_created_at, d.created_at), 'MM MON') AS month,
                COALESCE(NULLIF(u.full_name, ''), d.assigned_by_bitrix_id, 'Sin asesor') AS advisor,
                COALESCE(SUM(d.opportunity), 0) AS total
            FROM bitrix.deals d
            JOIN bitrix.pipelines pipeline ON pipeline.id = d.pipeline_id
            LEFT JOIN bitrix.pipeline_stages stage
                ON stage.pipeline_id = pipeline.id AND stage.bitrix_stage_id = d.stage_id
            LEFT JOIN bitrix.users u
                ON u.connection_id = d.connection_id AND u.bitrix_id = d.assigned_by_bitrix_id
            WHERE pipeline.category_id = 72
                AND UPPER(COALESCE(stage.name, '')) IN ('CUENTA PAGADA CUENTAS DE COBRO', 'VERIFICADO X PAGAR')
                AND EXTRACT(YEAR FROM COALESCE(d.bitrix_created_at, d.created_at)) = @year
            GROUP BY 1, 2
            ORDER BY total DESC;
            """;

        var leadership = new List<object>();
        var commissions = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(leadershipSql, connection))
        {
            command.Parameters.AddWithValue("yearText", year.ToString(CultureInfo.InvariantCulture));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                leadership.Add(new
                {
                    month = reader.GetString(0),
                    leader = reader.GetString(1),
                    coordinator = reader.GetString(2),
                    totalAchieved = reader.GetDecimal(3)
                });
            }
        }

        await using (var command = new NpgsqlCommand(commissionsSql, connection))
        {
            command.Parameters.AddWithValue("year", year);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                commissions.Add(new
                {
                    month = reader.GetString(0),
                    advisor = reader.GetString(1),
                    total = reader.GetDecimal(2)
                });
            }
        }

        return new { year, leadership, commissions };
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

    public static async Task<IResult> GetSyncHistoryAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT entity_type, status, records_read, records_written, created_at, finished_at
            FROM bitrix.sync_runs
            ORDER BY created_at DESC
            LIMIT 30;
            """;

        var rows = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new
            {
                entityType = reader.GetString(0),
                status = reader.GetString(1),
                recordsRead = reader.GetInt32(2),
                recordsWritten = reader.GetInt32(3),
                createdAt = reader.GetDateTime(4),
                finishedAt = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5)
            });
        }

        return Results.Ok(rows);
    }

    public static async Task<IResult> GetStageDistributionAsync(
        string pipeline,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.stage_id,
                COALESCE(s.name, d.stage_id, 'Sin etapa') AS stage_name,
                count(d.id)::integer AS deals_count
            FROM bitrix.deals d
            JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            LEFT JOIN bitrix.pipeline_stages s ON s.pipeline_id = p.id AND s.bitrix_stage_id = d.stage_id
            WHERE (@pipeline = 'all' OR p.slug = @pipeline)
            GROUP BY d.stage_id, s.name
            ORDER BY deals_count DESC, stage_name;
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
                stageId = reader.IsDBNull(0) ? null : reader.GetString(0),
                stageName = reader.GetString(1),
                dealsCount = reader.GetInt32(2)
            });
        }

        return Results.Ok(rows);
    }

    public static async Task<IResult> GetPipelineInventoryAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH pipeline_counts AS (
                SELECT pipeline_id, count(*)::integer AS deals_count
                FROM bitrix.deals
                GROUP BY pipeline_id
            ), inventory_stages AS (
                SELECT
                    stage.pipeline_id,
                    stage.bitrix_stage_id AS stage_id,
                    stage.name AS stage_name,
                    stage.sort_order,
                    stage.status_type,
                    false AS is_unmapped
                FROM bitrix.pipeline_stages stage

                UNION ALL

                SELECT
                    deal.pipeline_id,
                    deal.stage_id,
                    CASE
                        WHEN deal.stage_id IS NULL THEN 'Sin etapa'
                        ELSE 'No catalogada: ' || deal.stage_id
                    END AS stage_name,
                    NULL::integer AS sort_order,
                    NULL::text AS status_type,
                    true AS is_unmapped
                FROM bitrix.deals deal
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM bitrix.pipeline_stages stage
                    WHERE stage.pipeline_id = deal.pipeline_id
                      AND stage.bitrix_stage_id IS NOT DISTINCT FROM deal.stage_id
                )
                GROUP BY deal.pipeline_id, deal.stage_id
            ), stage_counts AS (
                SELECT pipeline_id, stage_id, count(*)::integer AS deals_count
                FROM bitrix.deals
                GROUP BY pipeline_id, stage_id
            )
            SELECT
                pipeline.id,
                pipeline.slug,
                pipeline.name,
                pipeline.category_id,
                pipeline.domain,
                COALESCE(pipeline_count.deals_count, 0) AS pipeline_deals_count,
                inventory_stage.pipeline_id AS inventory_pipeline_id,
                inventory_stage.stage_id,
                inventory_stage.stage_name,
                inventory_stage.sort_order,
                inventory_stage.status_type,
                COALESCE(stage_count.deals_count, 0) AS stage_deals_count,
                COALESCE(inventory_stage.is_unmapped, false) AS is_unmapped
            FROM bitrix.pipelines pipeline
            LEFT JOIN pipeline_counts pipeline_count ON pipeline_count.pipeline_id = pipeline.id
            LEFT JOIN inventory_stages inventory_stage ON inventory_stage.pipeline_id = pipeline.id
            LEFT JOIN stage_counts stage_count
                ON stage_count.pipeline_id = pipeline.id
                AND stage_count.stage_id IS NOT DISTINCT FROM inventory_stage.stage_id
            WHERE pipeline.is_active = true
            ORDER BY
                pipeline.sync_order,
                pipeline.category_id,
                inventory_stage.is_unmapped,
                inventory_stage.sort_order NULLS LAST,
                inventory_stage.stage_name;
            """;

        var pipelines = new Dictionary<Guid, PipelineInventoryItem>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var pipelineId = reader.GetGuid(0);
            if (!pipelines.TryGetValue(pipelineId, out var pipeline))
            {
                pipeline = new PipelineInventoryItem(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetString(4),
                    reader.GetInt32(5));
                pipelines.Add(pipelineId, pipeline);
            }

            if (!reader.IsDBNull(6))
            {
                pipeline.Stages.Add(new StageInventoryItem(
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.GetInt32(11),
                    reader.GetBoolean(12)));
            }
        }

        var items = pipelines.Values.ToList();
        return Results.Ok(new
        {
            totalDeals = items.Sum(item => item.DealsCount),
            pipelines = items
        });
    }

    public static async Task<IResult> GetResponsibleDistributionAsync(
        string pipeline,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.assigned_by_bitrix_id,
                COALESCE(u.full_name, 'Sin responsable') AS responsible_name,
                count(d.id)::integer AS deals_count
            FROM bitrix.deals d
            JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            LEFT JOIN bitrix.users u ON u.connection_id = d.connection_id AND u.bitrix_id = d.assigned_by_bitrix_id
            WHERE (@pipeline = 'all' OR p.slug = @pipeline)
            GROUP BY d.assigned_by_bitrix_id, u.full_name
            ORDER BY deals_count DESC, responsible_name
            LIMIT 20;
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
                responsibleId = reader.IsDBNull(0) ? null : reader.GetString(0),
                responsibleName = reader.GetString(1),
                dealsCount = reader.GetInt32(2)
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

    private sealed record StageInventoryItem(
        string? StageId,
        string StageName,
        int? SortOrder,
        string? StatusType,
        int DealsCount,
        bool IsUnmapped);

    private sealed record PipelineInventoryItem(
        string Slug,
        string Name,
        int CategoryId,
        string Domain,
        int DealsCount)
    {
        public List<StageInventoryItem> Stages { get; } = [];
    }
}
