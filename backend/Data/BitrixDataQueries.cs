using System.Globalization;
using Npgsql;

namespace InformesAvanzar.Api.Data;

public static class BitrixDataQueries
{
    public static async Task EnsureManagementPipelinesAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO bitrix.pipelines (slug, name, category_id, domain, sync_order, is_active)
            VALUES
                ('1116_comercial', '1116 Comercial', 30, 'comercial', 41, true),
                ('1116_operativa', '1116 Operativa', 32, 'operaciones', 42, true),
                ('lp_2445_operativa', 'LP-2445 Operativa', 248, 'operaciones', 43, true),
                ('informes_bi_builder', 'Informes BI Builder', 224, 'comercial', 44, true),
                ('ins_libranza', 'INS Libranza', 107, 'operaciones', 50, true),
                ('ins_embargos', 'INS Embargos', 109, 'operaciones', 52, true),
                ('pqrfs', 'PQRFS', 97, 'servicio_cliente', 55, true),
                ('seguros_operativa', 'Seguros Operativa', 256, 'seguros', 60, true),
                ('seguros_comercial', 'Seguros Comercial', 278, 'seguros', 62, true)
            ON CONFLICT DO NOTHING;

            UPDATE bitrix.pipelines pipeline
            SET name = source.name, domain = source.domain, sync_order = source.sync_order, is_active = true
            FROM (VALUES
                (30, '1116 Comercial', 'comercial', 41),
                (32, '1116 Operativa', 'operaciones', 42),
                (248, 'LP-2445 Operativa', 'operaciones', 43),
                (224, 'Informes BI Builder', 'comercial', 44),
                (107, 'INS Libranza', 'operaciones', 50),
                (109, 'INS Embargos', 'operaciones', 52),
                (97, 'PQRFS', 'servicio_cliente', 55),
                (256, 'Seguros Operativa', 'seguros', 60),
                (278, 'Seguros Comercial', 'seguros', 62)
            ) AS source(category_id, name, domain, sync_order)
            WHERE pipeline.category_id = source.category_id;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

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

    public static async Task<object> GetPipelinesAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT slug, name, category_id, domain, sync_order
            FROM bitrix.pipelines
            ORDER BY category_id, slug;
            """;

        var rows = new List<object>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new
            {
                slug = reader.GetString(0),
                name = reader.GetString(1),
                categoryId = reader.GetInt32(2),
                domain = reader.IsDBNull(3) ? null : reader.GetString(3),
                syncOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
            });
        }

        return rows;
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
                    CASE
                        WHEN p.category_id = 28 THEN 'PNNC'
                        WHEN p.category_id = 10 THEN 'RCH'
                        WHEN p.category_id = 32 THEN '1116'
                        WHEN p.category_id = 248 THEN 'LP-2445'
                    END AS pipeline,
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
                        p.category_id IN (10, 28, 32, 248)
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
            SELECT month, pipeline, advisor, COALESCE(SUM(amount), 0) AS total_achieved
            FROM radicated
            WHERE month IS NOT NULL
            GROUP BY month, pipeline, advisor
            ORDER BY total_achieved DESC, month, pipeline, advisor;
            """;

        var items = new List<object>();
        decimal total = 0;
        decimal annualGoal = 0;
        var monthlyGoals = new List<object>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year.ToString(CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var amount = reader.GetDecimal(3);
            total += amount;
            items.Add(new
            {
                month = reader.GetString(0),
                pipeline = reader.GetString(1),
                advisor = reader.GetString(2),
                totalAchieved = amount
            });
        }

        const string goalsSql = """
            SELECT
                d.title AS month,
                CASE
                    WHEN UPPER(COALESCE(stage.name, '')) = 'METAS INS COMERCIAL' THEN 'PNNC'
                    WHEN UPPER(COALESCE(stage.name, '')) = 'METAS RCH COMERCIAL' THEN 'RCH'
                    WHEN UPPER(COALESCE(stage.name, '')) = 'METAS 1116 COMERCIAL' THEN '1116'
                    WHEN UPPER(COALESCE(stage.name, '')) = 'METAS LP-2445 COMERCIAL' THEN 'LP-2445'
                END AS pipeline,
                COALESCE(SUM(d.opportunity), 0) AS goal
            FROM bitrix.deals d
            JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
            LEFT JOIN bitrix.pipeline_stages stage
                ON stage.pipeline_id = p.id
                AND stage.bitrix_stage_id = d.stage_id
            WHERE p.category_id = 224
              AND UPPER(COALESCE(stage.name, '')) IN ('METAS INS COMERCIAL', 'METAS RCH COMERCIAL', 'METAS 1116 COMERCIAL', 'METAS LP-2445 COMERCIAL')
              AND (
                  snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                  OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                      WHEN '2024' THEN '37206' WHEN '2025' THEN '37036' WHEN '2026' THEN '39138'
                  END
              )
            GROUP BY d.title, 2
            ORDER BY d.title, 2;
            """;

        await reader.DisposeAsync();
        await using (var goalsCommand = new NpgsqlCommand(goalsSql, connection))
        {
            goalsCommand.Parameters.AddWithValue("year", year.ToString(CultureInfo.InvariantCulture));
            await using var goalsReader = await goalsCommand.ExecuteReaderAsync(cancellationToken);
            while (await goalsReader.ReadAsync(cancellationToken))
            {
                var goal = goalsReader.GetDecimal(2);
                annualGoal += goal;
                monthlyGoals.Add(new { month = goalsReader.GetString(0), pipeline = goalsReader.GetString(1), goal });
            }
        }

        return new { year, totalAchieved = total, annualGoal, monthlyGoals, items };
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

        const string possibleCloseGeneralSql = """
            WITH classified AS (
                SELECT
                    CASE
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) LIKE '%REVISION DE LIDER%' THEN '01 Revisión líder'
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) LIKE '%RADICACION POR VALIDAR%' THEN '02 Radicación por validar'
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) ~ '(DOCUMENTACION PENDIENTE COMERCIAL|DOCUMENTOS PENDIENTES)' THEN '03 Documentación pendiente'
                        WHEN UPPER(TRANSLATE(COALESCE(s.name, d.stage_id, ''), 'ÁÉÍÓÚÜÑáéíóúüñ', 'AEIOUUNAEIOUUN')) ~ '(DOCUMENTACION SUBSANADA COMERCIAL|DOCUMENTOS SUBSANADOS|DOCUMENTOS SUBSANDADOS COMERCIAL)' THEN '04 Documentación subsanada'
                    END AS stage,
                    CASE
                        WHEN p.category_id IN (26, 28) THEN 'PNNC'
                        WHEN p.category_id IN (8, 10) THEN 'RCH'
                        WHEN p.category_id IN (30, 32) THEN '1116'
                    END AS pipeline,
                    d.opportunity
                FROM bitrix.deals d
                JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                LEFT JOIN bitrix.pipeline_stages s ON s.pipeline_id = p.id AND s.bitrix_stage_id = d.stage_id
                WHERE p.category_id IN (8, 10, 26, 28, 30, 32)
            )
            SELECT stage, pipeline, COALESCE(SUM(opportunity),0) AS amount, COUNT(*)::bigint AS cases
            FROM classified
            WHERE stage IS NOT NULL
            GROUP BY stage, pipeline
            ORDER BY stage, pipeline;
            """;

        var advisors = new List<object>();
        var stages = new List<object>();
        var departments = new List<object>();
        var possibleClosePnnc = new List<object>();
        var possibleCloseGeneral = new List<object>();
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

        await using (var command = new NpgsqlCommand(possibleCloseGeneralSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                possibleCloseGeneral.Add(new { stage = reader.GetString(0), pipeline = reader.GetString(1), amount = reader.GetDecimal(2), cases = reader.GetInt64(3) });
        }

        return new { year, advisors, stages, departments, possibleClosePnnc, possibleCloseGeneral };
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
            WITH RECURSIVE latest_users AS (
                SELECT DISTINCT ON (connection_id, bitrix_id) connection_id, bitrix_id, payload
                FROM bitrix.raw_payloads
                WHERE entity_type = 'user'
                ORDER BY connection_id, bitrix_id, received_at DESC
            ), user_departments AS (
                SELECT DISTINCT
                    u.connection_id,
                    u.bitrix_id,
                    u.full_name,
                    (jsonb_array_elements_text(payload.payload -> 'UF_DEPARTMENT'))::bigint AS department_id
                FROM bitrix.users u
                JOIN latest_users payload ON payload.connection_id = u.connection_id AND payload.bitrix_id = u.bitrix_id
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
                        WHEN pipeline.category_id = 10 THEN 'RCH'
                        WHEN pipeline.category_id = 28 THEN 'PNNC'
                    END AS commercial_line,
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
                COALESCE(NULLIF(people.full_name, ''), radicated.assigned_by_bitrix_id, 'Sin asesor') AS advisor,
                COALESCE(people.leader, 'Sin líder') AS leader,
                COALESCE(people.coordinator, 'Sin coordinador') AS coordinator,
                radicated.commercial_line,
                COALESCE(SUM(radicated.amount), 0) AS total_achieved
            FROM radicated
            LEFT JOIN people
                ON people.connection_id = radicated.connection_id
                AND people.bitrix_id = radicated.assigned_by_bitrix_id
            WHERE radicated.month IS NOT NULL
            GROUP BY radicated.month, people.full_name, radicated.assigned_by_bitrix_id, people.leader, people.coordinator, radicated.commercial_line
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

        const string relationshipsSql = """
            WITH RECURSIVE latest_users AS (
                SELECT DISTINCT ON (connection_id, bitrix_id) connection_id, bitrix_id, payload
                FROM bitrix.raw_payloads
                WHERE entity_type = 'user'
                ORDER BY connection_id, bitrix_id, received_at DESC
            ), user_departments AS (
                SELECT DISTINCT
                    u.connection_id,
                    u.bitrix_id,
                    u.full_name,
                    (jsonb_array_elements_text(payload.payload -> 'UF_DEPARTMENT'))::bigint AS department_id
                FROM bitrix.users u
                JOIN latest_users payload ON payload.connection_id = u.connection_id AND payload.bitrix_id = u.bitrix_id
                WHERE u.active = true
                  AND jsonb_typeof(payload.payload -> 'UF_DEPARTMENT') = 'array'
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
            )
            SELECT
                COALESCE(NULLIF(full_name, ''), bitrix_id, 'Sin asesor') AS advisor,
                COALESCE(MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. LIDER%'), 'Sin líder') AS leader,
                COALESCE(MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. COOR%'), 'Sin coordinador') AS coordinator,
                CASE
                    WHEN BOOL_OR(UPPER(name) LIKE '%RCH%') THEN 'RCH'
                    WHEN BOOL_OR(UPPER(name) LIKE '%PNNC%' OR UPPER(name) LIKE '%INSOLV%') THEN 'PNNC'
                    ELSE 'COMERCIAL'
                END AS commercial_line
            FROM hierarchy
            GROUP BY connection_id, bitrix_id, full_name
            HAVING MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. COOR%') IS NOT NULL
                OR MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. LIDER%') IS NOT NULL
            ORDER BY coordinator, leader, advisor;
            """;

        var leadership = new List<object>();
        var commissions = new List<object>();
        var relationships = new List<object>();
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
                    advisor = reader.GetString(1),
                    leader = reader.GetString(2),
                    coordinator = reader.GetString(3),
                    commercialLine = reader.GetString(4),
                    totalAchieved = reader.GetDecimal(5)
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

        await using (var command = new NpgsqlCommand(relationshipsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                relationships.Add(new
                {
                    advisor = reader.GetString(0),
                    leader = reader.GetString(1),
                    coordinator = reader.GetString(2),
                    commercialLine = reader.GetString(3)
                });
            }
        }

        return new { year, leadership, commissions, relationships };
    }

    public static async Task<object> GetCommercialFilterHierarchyAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH RECURSIVE latest_users AS (
                SELECT DISTINCT ON (connection_id, bitrix_id) connection_id, bitrix_id, payload
                FROM bitrix.raw_payloads
                WHERE entity_type = 'user'
                ORDER BY connection_id, bitrix_id, received_at DESC
            ), user_departments AS (
                SELECT DISTINCT
                    u.connection_id,
                    u.bitrix_id,
                    u.full_name,
                    (jsonb_array_elements_text(payload.payload -> 'UF_DEPARTMENT'))::bigint AS department_id
                FROM bitrix.users u
                JOIN latest_users payload ON payload.connection_id = u.connection_id AND payload.bitrix_id = u.bitrix_id
                WHERE u.active = true
                  AND jsonb_typeof(payload.payload -> 'UF_DEPARTMENT') = 'array'
            ), hierarchy AS (
                SELECT ud.connection_id, ud.bitrix_id, ud.full_name, department.id, department.name, department.parent_id, 1 AS depth
                FROM user_departments ud
                JOIN bitrix.departments department ON department.id = ud.department_id
                UNION ALL
                SELECT hierarchy.connection_id, hierarchy.bitrix_id, hierarchy.full_name, parent.id, parent.name, parent.parent_id, hierarchy.depth + 1
                FROM hierarchy
                JOIN bitrix.departments parent ON parent.id = hierarchy.parent_id
                WHERE hierarchy.depth < 8
            )
            SELECT
                COALESCE(NULLIF(full_name, ''), bitrix_id, 'Sin asesor') AS advisor,
                COALESCE(MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. LIDER%'), 'Sin líder') AS leader,
                COALESCE(MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. COOR%'), 'Sin coordinador') AS coordinator,
                CASE
                    WHEN BOOL_OR(UPPER(name) LIKE '%RCH%') THEN 'RCH'
                    WHEN BOOL_OR(UPPER(name) LIKE '%PNNC%' OR UPPER(name) LIKE '%INSOLV%') THEN 'PNNC'
                    ELSE 'COMERCIAL'
                END AS commercial_line
            FROM hierarchy
            GROUP BY connection_id, bitrix_id, full_name
            HAVING MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. COOR%') IS NOT NULL
                OR MAX(name) FILTER (WHERE UPPER(name) LIKE '%EQ. LIDER%') IS NOT NULL
            ORDER BY coordinator, leader, advisor;
            """;

        var items = new List<object>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new
            {
                advisor = reader.GetString(0),
                leader = reader.GetString(1),
                coordinator = reader.GetString(2),
                commercialLine = reader.GetString(3)
            });
        }
        return new { items };
    }

    public static async Task<object> GetManagementCommercialComplianceAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH db AS (
                SELECT
                    CASE
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22560' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ENERO%' THEN '01 ENE'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22562' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%FEBRERO%' THEN '02 FEB'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39144' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MARZO%' THEN '03 MAR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39146' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ABRIL%' THEN '04 ABR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39148' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MAYO%' THEN '05 MAY'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39150' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JUNIO%' THEN '06 JUN'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39152' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JULIO%' THEN '07 JUL'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39154' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%AGOSTO%' THEN '08 AGO'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39156' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%SEPTIEMBRE%' THEN '09 SEP'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39158' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%OCTUBRE%' THEN '10 OCT'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39160' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%NOVIEMBRE%' THEN '11 NOV'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39162' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%DICIEMBRE%' THEN '12 DIC'
                        ELSE '13 OTRO'
                    END AS month,
                    SUM(d.opportunity) AS cases_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                WHERE
                    (
                        p.category_id IN (10, 28, 32, 248)
                        OR UPPER(p.name) IN ('RCH OPERATIVA', 'PNNC OPERATIVA', '1116 OPERATIVA', 'LP OPERATIVA 2445')
                    )
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                GROUP BY 1
            ),
            mt AS (
                SELECT
                    d.title AS month,
                    SUM(d.opportunity) AS target_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE
                    p.category_id = 224
                    AND COALESCE(stage.name, d.stage_id) IN ('Metas INS Comercial', 'Metas RCH Comercial', 'Metas 1116 Comercial')
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                    AND d.title IN ('01 ENE', '02 FEB', '03 MAR', '04 ABR', '05 MAY', '06 JUN', '07 JUL', '08 AGO', '09 SEP', '10 OCT', '11 NOV', '12 DIC')
                GROUP BY d.title
            )
            SELECT
                COALESCE(SUM(db.cases_amount), 0) AS achieved,
                COALESCE(SUM(mt.target_amount), 0) AS target,
                COALESCE(SUM(db.cases_amount), 0) / NULLIF(SUM(mt.target_amount), 0) AS compliance
            FROM db
            INNER JOIN mt ON mt.month = db.month
            WHERE db.month <> '13 OTRO';
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year.ToString(CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new { year, achieved = 0m, target = 0m, compliance = (decimal?)null };
        }

        return new
        {
            year,
            achieved = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0),
            target = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
            compliance = reader.IsDBNull(2) ? (decimal?)null : reader.GetDecimal(2)
        };
    }

    public static async Task<object> GetManagementCommercialMonthlyComplianceAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH db AS (
                SELECT
                    CASE
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22560' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ENERO%' THEN '01 ENE'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22562' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%FEBRERO%' THEN '02 FEB'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39144' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MARZO%' THEN '03 MAR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39146' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ABRIL%' THEN '04 ABR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39148' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MAYO%' THEN '05 MAY'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39150' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JUNIO%' THEN '06 JUN'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39152' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JULIO%' THEN '07 JUL'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39154' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%AGOSTO%' THEN '08 AGO'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39156' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%SEPTIEMBRE%' THEN '09 SEP'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39158' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%OCTUBRE%' THEN '10 OCT'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39160' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%NOVIEMBRE%' THEN '11 NOV'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39162' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%DICIEMBRE%' THEN '12 DIC'
                        ELSE '13 OTRO'
                    END AS month,
                    SUM(d.opportunity) AS cases_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                WHERE
                    (
                        p.category_id IN (10, 28, 32, 248)
                        OR UPPER(p.name) IN ('RCH OPERATIVA', 'PNNC OPERATIVA', '1116 OPERATIVA', 'LP OPERATIVA 2445')
                    )
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                GROUP BY 1
            ),
            mt AS (
                SELECT
                    d.title AS month,
                    SUM(d.opportunity) AS target_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE
                    p.category_id = 224
                    AND COALESCE(stage.name, d.stage_id) IN ('Metas INS Comercial', 'Metas RCH Comercial', 'Metas 1116 Comercial')
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                    AND d.title IN ('01 ENE', '02 FEB', '03 MAR', '04 ABR', '05 MAY', '06 JUN', '07 JUL', '08 AGO', '09 SEP', '10 OCT', '11 NOV', '12 DIC')
                GROUP BY d.title
            )
            SELECT
                db.month,
                mt.target_amount,
                db.cases_amount,
                db.cases_amount / NULLIF(mt.target_amount, 0) AS compliance
            FROM db
            INNER JOIN mt ON mt.month = db.month
            WHERE db.month <> '13 OTRO'
            ORDER BY compliance DESC NULLS LAST, db.month;
            """;

        var rows = new List<object>();
        decimal totalTarget = 0;
        decimal totalAchieved = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year.ToString(CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var target = reader.GetDecimal(1);
            var achieved = reader.GetDecimal(2);
            totalTarget += target;
            totalAchieved += achieved;

            rows.Add(new
            {
                month = reader.GetString(0),
                target,
                achieved,
                compliance = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3)
            });
        }

        return new
        {
            year,
            totalTarget,
            totalAchieved,
            totalCompliance = totalTarget == 0 ? (decimal?)null : totalAchieved / totalTarget,
            rows
        };
    }

    public static async Task<object> GetManagementPossibleCloseAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH mapped AS (
                SELECT
                    CASE
                        WHEN UPPER(COALESCE(stage.name, d.stage_id)) IN ('REVISIÓN DE LÍDER', 'REVISION DE LIDER') THEN '01 Revisión líder'
                        WHEN UPPER(COALESCE(stage.name, d.stage_id)) IN ('RADICACIÓN POR VALIDAR', 'RADICACION POR VALIDAR') THEN '02 Radicación por validar'
                        WHEN UPPER(COALESCE(stage.name, d.stage_id)) IN ('DOCUMENTACIÓN PENDIENTE COMERCIAL', 'DOCUMENTACION PENDIENTE COMERCIAL', 'DOCUMENTOS PENDIENTES') THEN '03 Documentación pendiente'
                        WHEN UPPER(COALESCE(stage.name, d.stage_id)) IN ('DOCUMENTACIÓN SUBSANADA COMERCIAL', 'DOCUMENTACION SUBSANADA COMERCIAL', 'DOCUMENTOS SUBSANADOS', 'DOCUMENTOS SUBSANDADOS COMERCIAL') THEN '04 Documentación subsanada'
                    END AS stage,
                    CASE
                        WHEN p.category_id IN (30, 32) THEN '1116'
                        WHEN p.category_id IN (26, 28) THEN 'PNNC'
                        WHEN p.category_id IN (8, 10) THEN 'RCH'
                    END AS pipeline,
                    d.opportunity AS amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id IN (8, 10, 26, 28, 30, 32)
                  AND UPPER(COALESCE(d.title, '')) NOT LIKE '%PRUEBA%'
            ),
            grouped AS (
                SELECT stage, pipeline, SUM(amount) AS amount
                FROM mapped
                WHERE stage IS NOT NULL
                  AND pipeline IS NOT NULL
                GROUP BY stage, pipeline
            )
            SELECT
                stage,
                COALESCE(SUM(amount) FILTER (WHERE pipeline = '1116'), 0) AS amount_1116,
                COALESCE(SUM(amount) FILTER (WHERE pipeline = 'PNNC'), 0) AS amount_pnnc,
                COALESCE(SUM(amount) FILTER (WHERE pipeline = 'RCH'), 0) AS amount_rch
            FROM grouped
            GROUP BY stage
            ORDER BY stage;
            """;

        var rows = new List<object>();
        decimal total1116 = 0;
        decimal totalPnnc = 0;
        decimal totalRch = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var amount1116 = reader.GetDecimal(1);
            var amountPnnc = reader.GetDecimal(2);
            var amountRch = reader.GetDecimal(3);
            total1116 += amount1116;
            totalPnnc += amountPnnc;
            totalRch += amountRch;

            rows.Add(new
            {
                stage = reader.GetString(0),
                amount1116,
                amountPnnc,
                amountRch
            });
        }

        return new
        {
            rows,
            totals = new
            {
                amount1116 = total1116,
                amountPnnc = totalPnnc,
                amountRch = totalRch
            }
        };
    }

    public static async Task<object> GetManagementPnncDetailComplianceAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH db AS (
                SELECT
                    CASE
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22560' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ENERO%' THEN '01 ENE'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22562' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%FEBRERO%' THEN '02 FEB'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39144' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MARZO%' THEN '03 MAR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39146' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ABRIL%' THEN '04 ABR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39148' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MAYO%' THEN '05 MAY'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39150' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JUNIO%' THEN '06 JUN'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39152' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JULIO%' THEN '07 JUL'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39154' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%AGOSTO%' THEN '08 AGO'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39156' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%SEPTIEMBRE%' THEN '09 SEP'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39158' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%OCTUBRE%' THEN '10 OCT'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39160' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%NOVIEMBRE%' THEN '11 NOV'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39162' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%DICIEMBRE%' THEN '12 DIC'
                        ELSE '13 OTRO'
                    END AS month,
                    SUM(d.opportunity) AS achieved_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                WHERE
                    p.category_id = 28
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                GROUP BY 1
            ),
            mt AS (
                SELECT
                    d.title AS month,
                    SUM(d.opportunity) AS target_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE
                    p.category_id = 224
                    AND COALESCE(stage.name, d.stage_id) = 'Metas INS Comercial'
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                    AND d.title IN ('01 ENE', '02 FEB', '03 MAR', '04 ABR', '05 MAY', '06 JUN', '07 JUL', '08 AGO', '09 SEP', '10 OCT', '11 NOV', '12 DIC')
                GROUP BY d.title
            )
            SELECT
                db.month,
                mt.target_amount,
                db.achieved_amount,
                db.achieved_amount / NULLIF(mt.target_amount, 0) AS compliance
            FROM db
            INNER JOIN mt ON mt.month = db.month
            WHERE db.month <> '13 OTRO'
            ORDER BY compliance DESC NULLS LAST, db.month;
            """;

        var rows = new List<object>();
        decimal totalTarget = 0;
        decimal totalAchieved = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year.ToString(CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var target = reader.GetDecimal(1);
            var achieved = reader.GetDecimal(2);
            totalTarget += target;
            totalAchieved += achieved;

            rows.Add(new
            {
                month = reader.GetString(0),
                target,
                achieved,
                compliance = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3)
            });
        }

        return new
        {
            year,
            totalTarget,
            totalAchieved,
            totalCompliance = totalTarget == 0 ? (decimal?)null : totalAchieved / totalTarget,
            rows
        };
    }

    public static async Task<object> GetManagementRchAccumulatedAverageAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH db AS (
                SELECT
                    CASE
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22560' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ENERO%' THEN '01 ENE'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22562' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%FEBRERO%' THEN '02 FEB'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39144' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MARZO%' THEN '03 MAR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39146' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ABRIL%' THEN '04 ABR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39148' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MAYO%' THEN '05 MAY'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39150' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JUNIO%' THEN '06 JUN'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39152' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JULIO%' THEN '07 JUL'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39154' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%AGOSTO%' THEN '08 AGO'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39156' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%SEPTIEMBRE%' THEN '09 SEP'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39158' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%OCTUBRE%' THEN '10 OCT'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39160' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%NOVIEMBRE%' THEN '11 NOV'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39162' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%DICIEMBRE%' THEN '12 DIC'
                        ELSE '13 OTRO'
                    END AS month,
                    SUM(d.opportunity) AS cases_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                WHERE
                    p.category_id = 10
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                GROUP BY 1
            ),
            mt AS (
                SELECT
                    d.title AS month,
                    SUM(d.opportunity) AS target_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE
                    p.category_id = 224
                    AND COALESCE(stage.name, d.stage_id) = 'Metas RCH Comercial'
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @year
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @year
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                    AND d.title IN ('01 ENE', '02 FEB', '03 MAR', '04 ABR', '05 MAY', '06 JUN', '07 JUL', '08 AGO', '09 SEP', '10 OCT', '11 NOV', '12 DIC')
                GROUP BY d.title
            ),
            monthly AS (
                SELECT
                    db.month,
                    mt.target_amount,
                    db.cases_amount,
                    db.cases_amount / NULLIF(mt.target_amount, 0) AS compliance
                FROM db
                INNER JOIN mt ON mt.month = db.month
                WHERE db.month <> '13 OTRO'
            )
            SELECT
                month,
                target_amount,
                cases_amount,
                compliance
            FROM monthly
            ORDER BY month;
            """;

        var rows = new List<object>();
        decimal complianceTotal = 0;
        var complianceCount = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year.ToString(CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var compliance = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
            if (compliance.HasValue)
            {
                complianceTotal += compliance.Value;
                complianceCount++;
            }

            rows.Add(new
            {
                month = reader.GetString(0),
                target = reader.GetDecimal(1),
                achieved = reader.GetDecimal(2),
                compliance
            });
        }

        return new
        {
            year,
            average = complianceCount == 0 ? (decimal?)null : complianceTotal / complianceCount,
            rows
        };
    }

    public static async Task<object> GetManagementRchOperationalProcessesAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH source AS (
                SELECT
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') FROM 1 FOR 10)::date
                    END AS start_date,
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267643816', payload.payload ->> 'UF_CRM_1628267643816') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267643816', payload.payload ->> 'UF_CRM_1628267643816') FROM 1 FOR 10)::date
                    END AS end_date,
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('206', 'CAJA SOCIAL') THEN 'CAJA SOCIAL'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('208', 'COLPATRIA') THEN 'COLPATRIA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('210', 'DAVIVIENDA') THEN 'DAVIVIENDA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('298', 'COOPERATIVAS') THEN 'COOPERATIVAS'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('300', 'BANCOLOMBIA') THEN 'BANCOLOMBIA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('302', 'BANCO POPULAR') THEN 'BANCO POPULAR'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('304', 'AV VILLAS') THEN 'AV VILLAS'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('306', 'FONDO NACIONAL DEL AHORRO') THEN 'FONDO NACIONAL DEL AHORRO'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('308', 'LA HIPOTECARIA') THEN 'LA HIPOTECARIA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('310', 'ITAU') THEN 'ITAU'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('312', 'BANCO DE OCCIDENTE') THEN 'BANCO DE OCCIDENTE'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('23904', 'BANCO DE BOGOTÁ') THEN 'BANCO DE BOGOTÁ'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('23906', 'BBVA') THEN 'BBVA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('23922', 'SIN DEFINIR') THEN 'SIN DEFINIR'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('31046', 'NEQUI') THEN 'NEQUI'
                    END AS bank
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                WHERE p.category_id = 10
            ),
            inc AS (
                SELECT
                    EXTRACT(YEAR FROM start_date)::integer AS year,
                    CASE EXTRACT(MONTH FROM start_date)::integer
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                    END AS month,
                    bank,
                    COUNT(start_date) AS started_cases
                FROM source
                WHERE start_date IS NOT NULL
                  AND bank IS NOT NULL
                GROUP BY 1, 2, 3
            ),
            out AS (
                SELECT
                    EXTRACT(YEAR FROM end_date)::integer AS year,
                    CASE EXTRACT(MONTH FROM end_date)::integer
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                    END AS month,
                    bank,
                    COUNT(end_date) AS finished_cases
                FROM source
                WHERE end_date IS NOT NULL
                  AND bank IS NOT NULL
                GROUP BY 1, 2, 3
            ),
            joined AS (
                SELECT
                    inc.year,
                    inc.month,
                    inc.bank,
                    inc.started_cases,
                    out.finished_cases
                FROM inc
                INNER JOIN out
                    ON out.month = inc.month
                    AND out.year = inc.year
                    AND out.bank = inc.bank
                WHERE inc.year = @yearNumber
            ),
            monthly AS (
                SELECT
                    month,
                    SUM(started_cases) AS started_cases,
                    SUM(finished_cases) AS finished_cases
                FROM joined
                GROUP BY month
            )
            SELECT
                month,
                started_cases,
                finished_cases,
                NULL::text AS bank
            FROM monthly
            UNION ALL
            SELECT
                month,
                started_cases,
                finished_cases,
                bank
            FROM joined
            ORDER BY bank NULLS FIRST, month;
            """;

        var rows = new List<object>();
        var bankRows = new List<object>();
        long totalStarted = 0;
        long totalFinished = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("yearNumber", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var started = reader.GetInt64(1);
            var finished = reader.GetInt64(2);
            var bank = reader.IsDBNull(3) ? null : reader.GetString(3);

            if (bank is not null)
            {
                bankRows.Add(new
                {
                    month = reader.GetString(0),
                    started,
                    finished,
                    bank
                });
                continue;
            }

            totalStarted += started;
            totalFinished += finished;

            rows.Add(new
            {
                month = reader.GetString(0),
                started,
                finished
            });
        }

        return new
        {
            year,
            totalStarted,
            totalFinished,
            rows,
            bankRows
        };
    }

    public static async Task<object> GetManagementRchApprovedByBankAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH source AS (
                SELECT
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267643816', payload.payload ->> 'UF_CRM_1628267643816') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267643816', payload.payload ->> 'UF_CRM_1628267643816') FROM 1 FOR 10)::date
                    END AS approved_date,
                    d.opportunity,
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('206', 'CAJA SOCIAL') THEN 'CAJA SOCIAL'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('208', 'COLPATRIA') THEN 'COLPATRIA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('210', 'DAVIVIENDA') THEN 'DAVIVIENDA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('298', 'COOPERATIVAS') THEN 'COOPERATIVAS'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('300', 'BANCOLOMBIA') THEN 'BANCOLOMBIA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('302', 'BANCO POPULAR') THEN 'BANCO POPULAR'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('304', 'AV VILLAS') THEN 'AV VILLAS'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('306', 'FONDO NACIONAL DEL AHORRO') THEN 'FONDO NACIONAL DEL AHORRO'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('308', 'LA HIPOTECARIA') THEN 'LA HIPOTECARIA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('310', 'ITAU') THEN 'ITAU'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('312', 'BANCO DE OCCIDENTE') THEN 'BANCO DE OCCIDENTE'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('23904', 'BANCO DE BOGOTÃ') THEN 'BANCO DE BOGOTÃ'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('23906', 'BBVA') THEN 'BBVA'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('23922', 'SIN DEFINIR') THEN 'SIN DEFINIR'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584496415', payload.payload ->> 'UF_CRM_1584496415') IN ('31046', 'NEQUI') THEN 'NEQUI'
                    END AS bank
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE
                    p.category_id = 10
                    AND COALESCE(stage.name, d.stage_id) = 'APROBADO POR EL BANCO'
            ),
            bank_monthly AS (
                SELECT
                    EXTRACT(YEAR FROM approved_date)::integer AS year,
                    CASE EXTRACT(MONTH FROM approved_date)::integer
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                    END AS month,
                    bank,
                    COUNT(approved_date) AS cases_count,
                    SUM(opportunity) AS amount
                FROM source
                WHERE approved_date IS NOT NULL
                  AND EXTRACT(YEAR FROM approved_date)::integer = @yearNumber
                GROUP BY 1, 2, 3
            ),
            monthly AS (
                SELECT
                    month,
                    SUM(cases_count) AS cases_count,
                    SUM(amount) AS amount
                FROM bank_monthly
                GROUP BY month
            )
            SELECT
                month,
                cases_count,
                amount,
                NULL::text AS bank
            FROM monthly
            UNION ALL
            SELECT
                month,
                cases_count,
                amount,
                bank
            FROM bank_monthly
            ORDER BY bank NULLS FIRST, month;
            """;

        var rows = new List<object>();
        var bankRows = new List<object>();
        long totalCases = 0;
        decimal totalAmount = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("yearNumber", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var cases = reader.GetInt64(1);
            var amount = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
            var bank = reader.IsDBNull(3) ? null : reader.GetString(3);

            if (bank is not null)
            {
                bankRows.Add(new
                {
                    month = reader.GetString(0),
                    cases,
                    amount,
                    bank
                });
                continue;
            }

            totalCases += cases;
            totalAmount += amount;

            rows.Add(new
            {
                month = reader.GetString(0),
                cases,
                amount
            });
        }

        return new
        {
            year,
            totalCases,
            totalAmount,
            rows,
            bankRows
        };
    }

    public static async Task<object> GetManagementPnncOperationalProcesses2025Async(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH source AS (
                SELECT
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') FROM 1 FOR 10)::date
                    END AS start_date,
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419734', payload.payload ->> 'UF_CRM_1597419734') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419734', payload.payload ->> 'UF_CRM_1597419734') FROM 1 FOR 10)::date
                    END AS finish_date,
                    COALESCE(stage.name, d.stage_id) AS stage_name
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id = 28
            ),
            inc AS (
                SELECT
                    CASE EXTRACT(MONTH FROM start_date)::integer
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                    END AS month,
                    COUNT(start_date) AS started_cases
                FROM source
                WHERE start_date IS NOT NULL
                  AND EXTRACT(YEAR FROM start_date)::integer = @yearNumber
                GROUP BY EXTRACT(MONTH FROM start_date)::integer
            ),
            out AS (
                SELECT
                    CASE EXTRACT(MONTH FROM finish_date)::integer
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                    END AS month,
                    COUNT(finish_date) AS finished_cases
                FROM source
                WHERE finish_date IS NOT NULL
                  AND stage_name IN ('ACUERDO EXITOSO', 'LIQUIDACIÓN PATRIMONIAL')
                  AND EXTRACT(YEAR FROM finish_date)::integer = @yearNumber
                GROUP BY EXTRACT(MONTH FROM finish_date)::integer
            )
            SELECT
                inc.month,
                inc.started_cases,
                out.finished_cases
            FROM inc
            INNER JOIN out ON out.month = inc.month
            ORDER BY inc.month;
            """;

        var rows = new List<object>();
        long totalStarted = 0;
        long totalFinished = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("yearNumber", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var started = reader.GetInt64(1);
            var finished = reader.GetInt64(2);
            totalStarted += started;
            totalFinished += finished;

            rows.Add(new
            {
                month = reader.GetString(0),
                started,
                finished
            });
        }

        return new
        {
            year,
            totalStarted,
            totalFinished,
            rows
        };
    }

    public static async Task<object> GetManagementPnncOperationalManagementAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH deal_days AS (
                SELECT
                    d.id,
                    COALESCE(stage.name, d.stage_id) AS stage_name,
                    COALESCE(stage.sort_order, 9999) AS stage_order,
                    CASE
                        WHEN COALESCE(stage.name, d.stage_id) IN ('RADICACION POR VALIDAR ', 'RADICACION POR VALIDAR', 'RADICACIÓN POR VALIDAR')
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') FROM 1 FOR 10)::date
                                END
                            ) - 5
                        WHEN COALESCE(stage.name, d.stage_id) = 'DOCUMENTACIÓN SUBSANADA COMERCIAL'
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1654544609940', payload.payload ->> 'UF_CRM_1654544609940') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1654544609940', payload.payload ->> 'UF_CRM_1654544609940') FROM 1 FOR 10)::date
                                END
                            ) - 5
                        WHEN COALESCE(stage.name, d.stage_id) = 'ANÁLISIS JURÍDICO'
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1590601503', payload.payload ->> 'UF_CRM_1590601503') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1590601503', payload.payload ->> 'UF_CRM_1590601503') FROM 1 FOR 10)::date
                                END
                            ) - 14
                        WHEN COALESCE(stage.name, d.stage_id) IN ('FIRMA OTRO SÍ', 'FIRMA OTRO SI')
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419361', payload.payload ->> 'UF_CRM_1597419361') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419361', payload.payload ->> 'UF_CRM_1597419361') FROM 1 FOR 10)::date
                                END
                            ) - 3
                        WHEN COALESCE(stage.name, d.stage_id) = 'DOCUMENTACION SUBSANADA OPERATIVA'
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1647621561172', payload.payload ->> 'UF_CRM_1647621561172') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1647621561172', payload.payload ->> 'UF_CRM_1647621561172') FROM 1 FOR 10)::date
                                END
                            ) - 6
                        WHEN COALESCE(stage.name, d.stage_id) = 'SOLICITUD ENVIADA'
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1704395777981', payload.payload ->> 'UF_CRM_1704395777981') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1704395777981', payload.payload ->> 'UF_CRM_1704395777981') FROM 1 FOR 10)::date
                                END
                            ) - 4
                        WHEN COALESCE(stage.name, d.stage_id) = 'RADICADO CENTRO CONCILIACION'
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419674', payload.payload ->> 'UF_CRM_1597419674') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419674', payload.payload ->> 'UF_CRM_1597419674') FROM 1 FOR 10)::date
                                END
                            ) - 35
                        WHEN COALESCE(stage.name, d.stage_id) = 'CITA DE PRESENTACION'
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651070289441', payload.payload ->> 'UF_CRM_1651070289441') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651070289441', payload.payload ->> 'UF_CRM_1651070289441') FROM 1 FOR 10)::date
                                END
                            ) - 24
                        WHEN COALESCE(stage.name, d.stage_id) = 'PAGO RECIBIDO'
                            THEN CURRENT_DATE - (
                                CASE
                                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') ~ '^\d{4}-\d{2}-\d{2}'
                                        THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') FROM 1 FOR 10)::date
                                END
                            ) - 10
                    END AS days_out_of_management
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE
                    p.category_id = 28
                    AND COALESCE(stage.name, d.stage_id) IN (
                        'RADICACION POR VALIDAR ',
                        'RADICACION POR VALIDAR',
                        'RADICACIÓN POR VALIDAR',
                        'DOCUMENTACIÓN SUBSANADA COMERCIAL',
                        'ANÁLISIS JURÍDICO',
                        'FIRMA OTRO SÍ',
                        'FIRMA OTRO SI',
                        'DOCUMENTACION SUBSANADA OPERATIVA',
                        'SOLICITUD ENVIADA',
                        'RADICADO CENTRO CONCILIACION',
                        'CITA DE PRESENTACION',
                        'PAGO RECIBIDO'
                    )
            ),
            grouped AS (
                SELECT
                    stage_name,
                    COUNT(*) AS clients,
                    SUM(CASE WHEN days_out_of_management > 0 THEN 1 ELSE 0 END) AS out_of_management,
                    SUM(CASE WHEN days_out_of_management > 0 THEN days_out_of_management ELSE 0 END) AS out_of_management_days
                FROM deal_days
                GROUP BY stage_name
            ),
            total_cases AS (
                SELECT NULLIF(SUM(out_of_management), 0) AS total_out_of_management
                FROM grouped
            )
            SELECT
                stage_name,
                clients,
                out_of_management,
                ROUND(out_of_management * 100.0 / total_cases.total_out_of_management, 1) AS participation,
                out_of_management_days
            FROM grouped
            CROSS JOIN total_cases
            ORDER BY clients DESC, stage_name;
            """;

        var rows = new List<object>();
        long totalClients = 0;
        long totalOutOfManagement = 0;
        long totalOutOfManagementDays = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var clients = reader.GetInt64(1);
            var outOfManagement = reader.GetInt64(2);
            var outOfManagementDays = reader.GetInt64(4);
            totalClients += clients;
            totalOutOfManagement += outOfManagement;
            totalOutOfManagementDays += outOfManagementDays;

            rows.Add(new
            {
                stage = reader.GetString(0),
                clients,
                outOfManagement,
                participation = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3),
                outOfManagementDays
            });
        }

        return new
        {
            totalClients,
            totalOutOfManagement,
            totalParticipation = totalOutOfManagement == 0 ? (decimal?)null : 100m,
            totalOutOfManagementDays,
            rows
        };
    }

    public static async Task<object> GetManagementPnncOperationalInsolvencyTwoAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH deal_days AS (
                SELECT
                    d.id,
                    COALESCE(stage.name, d.stage_id) AS stage_name,
                    COALESCE(stage.sort_order, 9999) AS stage_order,
                    CASE
                        WHEN COALESCE(stage.name, d.stage_id) = 'DOCUMENTACIÓN PENDIENTE COMERCIAL' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'ANÁLISIS JURÍDICO PRIORITARIO' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'DIFERENCIA DE HONORARIOS' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'DOCUMENTACION PENDIENTE OPERATIVA' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'CUARENTENA' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'VACANCIA JUDICIAL' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'PENDIENTE PAGO' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'AUDIENCIA 1' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'AUDIENCIA 2' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'AUDIENCIA 3' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'AUDIENCIA 4' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'MAS DE 4 AUDIENCIAS' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'OBJECION' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'TERMINADO PENDIENTE AUTO' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'AUTORIZADO CARTERA' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'PEND. ACTA DE ACUERDO O CERTIFICADO' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'PENDIENTE FIRMA CLIENTE' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'ACUERDO CON NOVEDAD' THEN 1
                        WHEN COALESCE(stage.name, d.stage_id) = 'AUDIENCIA DE REFORMA' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'ACUERDO EXITOSO' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'LIQUIDACIÓN PATRIMONIAL' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'ACUERDO BILATERAL' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'CASOS SUSPENDIDOS' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'RETOMA' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'SOLICITUD DE BAJA' THEN 0
                        WHEN COALESCE(stage.name, d.stage_id) = 'DADO DE BAJA' THEN 0
                    END AS out_of_time
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id = 28
            )
            SELECT
                stage_name,
                MIN(stage_order) AS stage_order,
                COUNT(*) AS negotiations,
                COALESCE(SUM(out_of_time), 0) AS out_of_management
            FROM deal_days
            WHERE out_of_time IS NOT NULL
            GROUP BY stage_name
            ORDER BY
                CASE
                    WHEN stage_name = 'CASOS SUSPENDIDOS' THEN 1
                    WHEN stage_name = 'OBJECION' THEN 2
                    WHEN stage_name = 'AUDIENCIA 3' THEN 3
                    WHEN stage_name = 'PEND. ACTA DE ACUERDO O CERTIFICADO' THEN 4
                    WHEN stage_name LIKE '%PRIORITARIO' THEN 5
                    WHEN stage_name = 'AUDIENCIA 1' THEN 6
                    WHEN stage_name = 'DOCUMENTACION PENDIENTE OPERATIVA' THEN 7
                    WHEN stage_name = 'AUDIENCIA 2' THEN 8
                    WHEN stage_name = 'PENDIENTE PAGO' THEN 9
                    WHEN stage_name = 'SOLICITUD DE BAJA' THEN 10
                    ELSE 100
                END,
                stage_order,
                stage_name;
            """;

        var rows = new List<object>();
        long totalNegotiations = 0;
        long totalOutOfManagement = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var negotiations = reader.GetInt64(2);
            var outOfManagement = reader.GetInt64(3);
            totalNegotiations += negotiations;
            totalOutOfManagement += outOfManagement;

            rows.Add(new
            {
                stage = reader.GetString(0),
                stageOrder = reader.GetInt32(1),
                negotiations,
                outOfManagement
            });
        }

        return new
        {
            totalNegotiations,
            totalOutOfManagement,
            rows
        };
    }

    public static async Task<object> GetManagementPnncOperationalDetailAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH deal_days AS (
                SELECT
                    d.title AS name,
                    COALESCE(stage.name, d.stage_id) AS stage_name,
                    COALESCE(NULLIF(u.full_name, ''), d.assigned_by_bitrix_id, 'N/A') AS responsible,
                    COALESCE(d.opportunity, 0) AS total,
                    CASE
                        WHEN COALESCE(stage.name, d.stage_id) LIKE '%PRIORITARIO%' THEN 'Analisis Juridico Prioritario'
                        WHEN COALESCE(stage.name, d.stage_id) LIKE '%JUR%DICO%' THEN 'Analisis Juridico'
                        WHEN COALESCE(stage.name, d.stage_id) LIKE 'DOCUMENTACI%N SUBSANADA' THEN 'Documentacion Subsanada'
                        WHEN COALESCE(stage.name, d.stage_id) = 'SOLICITUD ENVIADA' THEN 'Solicitud Enviada'
                        WHEN COALESCE(stage.name, d.stage_id) = 'PAGO RECIBIDO' THEN 'Pago Recibido'
                    END AS mapped_stage,
                    CASE
                        WHEN COALESCE(stage.name, d.stage_id) LIKE '%JUR%DICO%' THEN
                            CASE
                                WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1590601503', payload.payload ->> 'UF_CRM_1590601503') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN (CURRENT_DATE - SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1590601503', payload.payload ->> 'UF_CRM_1590601503') FROM 1 FOR 10)::date) - 14
                            END
                        WHEN COALESCE(stage.name, d.stage_id) LIKE 'DOCUMENTACI%N SUBSANADA' THEN
                            CASE
                                WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1647621561172', payload.payload ->> 'UF_CRM_1647621561172') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN (CURRENT_DATE - SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1647621561172', payload.payload ->> 'UF_CRM_1647621561172') FROM 1 FOR 10)::date) - 6
                            END
                        WHEN COALESCE(stage.name, d.stage_id) = 'SOLICITUD ENVIADA' THEN
                            CASE
                                WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1704395777981', payload.payload ->> 'UF_CRM_1704395777981') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN (CURRENT_DATE - SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1704395777981', payload.payload ->> 'UF_CRM_1704395777981') FROM 1 FOR 10)::date) - 4
                            END
                        WHEN COALESCE(stage.name, d.stage_id) = 'PAGO RECIBIDO' THEN
                            CASE
                                WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN (CURRENT_DATE - SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') FROM 1 FOR 10)::date) - 10
                            END
                    END AS days_out_of_management,
                    COALESCE(stage.sort_order, 9999) AS stage_order,
                    d.bitrix_id
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                LEFT JOIN bitrix.users u
                    ON u.connection_id = d.connection_id
                    AND u.bitrix_id = d.assigned_by_bitrix_id
                WHERE p.category_id = 248
            )
            SELECT
                name,
                COALESCE(mapped_stage, 'N/A') AS mapped_stage,
                responsible,
                total,
                days_out_of_management
            FROM deal_days
            ORDER BY stage_order, CASE WHEN bitrix_id ~ '^\d+$' THEN bitrix_id::bigint END NULLS LAST, name;
            """;

        var rows = new List<object>();
        decimal totalAmount = 0m;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var total = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
            totalAmount += total;

            rows.Add(new
            {
                name = reader.GetString(0),
                stage = reader.GetString(1),
                responsible = reader.GetString(2),
                total,
                daysOutOfManagement = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
            });
        }

        return new
        {
            totalRows = rows.Count,
            totalAmount,
            rows
        };
    }

    public static async Task<object> GetManagementPnncLpCompliance2025Async(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH deal_dates AS (
                SELECT
                    COALESCE(stage.name, d.stage_id) AS stage_name,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266904138', payload.payload ->> 'UF_CRM_1628266904138') FROM 1 FOR 10)::date END AS start_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1654544609940', payload.payload ->> 'UF_CRM_1654544609940') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1654544609940', payload.payload ->> 'UF_CRM_1654544609940') FROM 1 FOR 10)::date END AS commercial_fixed_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1590601503', payload.payload ->> 'UF_CRM_1590601503') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1590601503', payload.payload ->> 'UF_CRM_1590601503') FROM 1 FOR 10)::date END AS legal_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419361', payload.payload ->> 'UF_CRM_1597419361') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419361', payload.payload ->> 'UF_CRM_1597419361') FROM 1 FOR 10)::date END AS addendum_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1647621561172', payload.payload ->> 'UF_CRM_1647621561172') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1647621561172', payload.payload ->> 'UF_CRM_1647621561172') FROM 1 FOR 10)::date END AS operational_fixed_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1704395777981', payload.payload ->> 'UF_CRM_1704395777981') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1704395777981', payload.payload ->> 'UF_CRM_1704395777981') FROM 1 FOR 10)::date END AS sent_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419674', payload.payload ->> 'UF_CRM_1597419674') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1597419674', payload.payload ->> 'UF_CRM_1597419674') FROM 1 FOR 10)::date END AS conciliation_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651070289441', payload.payload ->> 'UF_CRM_1651070289441') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651070289441', payload.payload ->> 'UF_CRM_1651070289441') FROM 1 FOR 10)::date END AS appointment_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') FROM 1 FOR 10)::date END AS payment_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266963127', payload.payload ->> 'UF_CRM_1628266963127') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628266963127', payload.payload ->> 'UF_CRM_1628266963127') FROM 1 FOR 10)::date END AS contracts_sent_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1654544553289', payload.payload ->> 'UF_CRM_1654544553289') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1654544553289', payload.payload ->> 'UF_CRM_1654544553289') FROM 1 FOR 10)::date END AS rejected_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267025513', payload.payload ->> 'UF_CRM_1628267025513') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267025513', payload.payload ->> 'UF_CRM_1628267025513') FROM 1 FOR 10)::date END AS admission_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267431791', payload.payload ->> 'UF_CRM_1628267431791') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267431791', payload.payload ->> 'UF_CRM_1628267431791') FROM 1 FOR 10)::date END AS claims_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267476819', payload.payload ->> 'UF_CRM_1628267476819') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267476819', payload.payload ->> 'UF_CRM_1628267476819') FROM 1 FOR 10)::date END AS objections_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267492835', payload.payload ->> 'UF_CRM_1628267492835') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267492835', payload.payload ->> 'UF_CRM_1628267492835') FROM 1 FOR 10)::date END AS award_project_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1623300067103', payload.payload ->> 'UF_CRM_1623300067103') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1623300067103', payload.payload ->> 'UF_CRM_1623300067103') FROM 1 FOR 10)::date END AS resolutory_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267593088', payload.payload ->> 'UF_CRM_1628267593088') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267593088', payload.payload ->> 'UF_CRM_1628267593088') FROM 1 FOR 10)::date END AS hearing_award_date,
                    CASE WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267618395', payload.payload ->> 'UF_CRM_1628267618395') ~ '^\d{4}-\d{2}-\d{2}' THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1628267618395', payload.payload ->> 'UF_CRM_1628267618395') FROM 1 FOR 10)::date END AS final_appointment_date
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage ON stage.pipeline_id = p.id AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id IN (28, 32)
            ),
            evaluated AS (
                SELECT
                    stage_name,
                    CASE
                        WHEN stage_name LIKE 'RADICACION POR VALIDAR%' AND start_date IS NOT NULL AND CURRENT_DATE - start_date > 5 THEN 1
                        WHEN stage_name = 'DOCUMENTACIÃ“N SUBSANADA COMERCIAL' AND commercial_fixed_date IS NOT NULL AND CURRENT_DATE - commercial_fixed_date > 5 THEN 1
                        WHEN stage_name = 'ANÃLISIS JURÃDICO' AND legal_date IS NOT NULL AND CURRENT_DATE - legal_date > 13 THEN 1
                        WHEN stage_name LIKE 'FIRMA OTRO S%' AND addendum_date IS NOT NULL AND CURRENT_DATE - addendum_date > 3 THEN 1
                        WHEN stage_name = 'DOCUMENTACION SUBSANADA OPERATIVA' AND operational_fixed_date IS NOT NULL AND CURRENT_DATE - operational_fixed_date > 6 THEN 1
                        WHEN stage_name = 'SOLICITUD ENVIADA' AND sent_date IS NOT NULL AND CURRENT_DATE - sent_date > 4 THEN 1
                        WHEN stage_name = 'RADICADO CENTRO CONCILIACION' AND conciliation_date IS NOT NULL AND CURRENT_DATE - conciliation_date > 35 THEN 1
                        WHEN stage_name = 'CITA DE PRESENTACION' AND appointment_date IS NOT NULL AND CURRENT_DATE - appointment_date > 24 THEN 1
                        WHEN stage_name = 'PAGO RECIBIDO' AND payment_date IS NOT NULL AND CURRENT_DATE - payment_date > 10 THEN 1
                        WHEN stage_name = 'VENTAS Y SEGUIMIENTO DE APERTURA' AND start_date IS NOT NULL AND CURRENT_DATE - start_date > 10 THEN 1
                        WHEN stage_name = 'CONTRATOS ENVIADOS' AND contracts_sent_date IS NOT NULL AND CURRENT_DATE - (contracts_sent_date + 10) > 10 THEN 1
                        WHEN stage_name = 'PROCESOS RECHAZADOS' AND rejected_date IS NOT NULL AND CURRENT_DATE - (rejected_date + 20) > 90 THEN 1
                        WHEN stage_name = 'CONTRATO FIRMADO' AND commercial_fixed_date IS NOT NULL AND CURRENT_DATE - commercial_fixed_date > 90 THEN 1
                        WHEN stage_name = '1. ADMISION Y NOMBRAMIENTO DEL LIQUIDADOR' AND admission_date IS NOT NULL AND CURRENT_DATE - admission_date > 180 THEN 1
                        WHEN stage_name = '1. ACTULIZACION DE ACREENCIAS ADMISION' AND claims_date IS NOT NULL AND CURRENT_DATE - (claims_date + 180) > 90 THEN 1
                        WHEN stage_name = '1. OBJECIONES U OBSERVACIONES' AND objections_date IS NOT NULL AND CURRENT_DATE - (objections_date + 270) > 180 THEN 1
                        WHEN stage_name = '1. PROYECTO DE ADJUDICACION' AND award_project_date IS NOT NULL AND CURRENT_DATE - (award_project_date + 450) > 30 THEN 1
                        WHEN stage_name = '2. ACUERDO RESOLUTORIO' AND resolutory_date IS NOT NULL AND CURRENT_DATE - (resolutory_date + 480) > 545 THEN 1
                        WHEN stage_name = '3. AUDIENCIA DE ADJUDICACION' AND hearing_award_date IS NOT NULL AND CURRENT_DATE - hearing_award_date > 150 THEN 1
                        WHEN stage_name = '3. SENTENCIA Y OFICIOS' AND hearing_award_date IS NOT NULL AND CURRENT_DATE - (hearing_award_date + 150) > 90 THEN 1
                        WHEN stage_name = '3. CITA DE FINALIZACION' AND final_appointment_date IS NOT NULL AND CURRENT_DATE - (final_appointment_date + 240) > 10 THEN 1
                        ELSE 0
                    END AS is_late
                FROM deal_dates
                WHERE stage_name IN (
                    'RADICACION POR VALIDAR ', 'DOCUMENTACIÃ“N SUBSANADA COMERCIAL', 'ANÃLISIS JURÃDICO', 'FIRMA OTRO SÃ',
                    'DOCUMENTACION SUBSANADA OPERATIVA', 'SOLICITUD ENVIADA', 'RADICADO CENTRO CONCILIACION', 'CITA DE PRESENTACION',
                    'PAGO RECIBIDO', 'VENTAS Y SEGUIMIENTO DE APERTURA', 'CONTRATOS ENVIADOS', 'PROCESOS RECHAZADOS',
                    'CONTRATO FIRMADO', '1. ADMISION Y NOMBRAMIENTO DEL LIQUIDADOR', '1. ACTULIZACION DE ACREENCIAS ADMISION',
                    '1. OBJECIONES U OBSERVACIONES', '1. PROYECTO DE ADJUDICACION', '2. ACUERDO RESOLUTORIO',
                    '3. AUDIENCIA DE ADJUDICACION', '3. SENTENCIA Y OFICIOS', '3. CITA DE FINALIZACION'
                )
            )
            SELECT
                COUNT(*) AS total_cases,
                COALESCE(SUM(is_late), 0) AS late_cases,
                1.0 - (COALESCE(SUM(is_late), 0)::numeric / NULLIF(COUNT(*), 0)) AS compliance
            FROM evaluated;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new { year = 2025, totalCases = 0L, lateCases = 0L, compliance = (decimal?)null };
        }

        return new
        {
            year = 2025,
            totalCases = reader.GetInt64(0),
            lateCases = reader.GetInt64(1),
            compliance = reader.IsDBNull(2) ? (decimal?)null : reader.GetDecimal(2)
        };
    }

    public static async Task<object> GetManagementLpMonthlyTasksAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH activity_base AS (
                SELECT
                    a.bitrix_id,
                    a.owner_bitrix_id AS deal_id,
                    'LP Operativa' AS pipeline,
                    COALESCE(NULLIF(u.full_name, ''), a.responsible_bitrix_id, 'Sin responsable') AS responsible,
                    COALESCE(
                        a.bitrix_created_at,
                        CASE WHEN payload.payload ->> 'CREATED' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'CREATED')::timestamptz END,
                        CASE WHEN payload.payload ->> 'DATE_CREATE' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'DATE_CREATE')::timestamptz END,
                        CASE WHEN payload.payload ->> 'START_TIME' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'START_TIME')::timestamptz END
                    ) AS created_at,
                    a.deadline_at,
                    CASE WHEN payload.payload ->> 'END_TIME' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'END_TIME')::timestamptz END AS ended_at
                FROM bitrix.activities a
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = a.raw_payload_id
                LEFT JOIN bitrix.users u ON u.connection_id = a.connection_id AND u.bitrix_id = a.responsible_bitrix_id
                WHERE
                    a.owner_type IN ('2', 'DEAL', 'deal')
                    AND a.responsible_bitrix_id IN ('9482', '16230', '2070')
                    AND a.type_id = '6'
            ),
            deal_tasks AS (
                SELECT
                    CASE
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 1 AND 4 THEN '01 ENE'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 5 AND 8 THEN '02 FEB'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 9 AND 13 THEN '03 MAR'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 14 AND 17 THEN '04 ABR'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 18 AND 21 THEN '05 MAY'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 22 AND 26 THEN '06 JUN'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 27 AND 30 THEN '07 JUL'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 31 AND 35 THEN '08 AGO'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 36 AND 39 THEN '09 SEP'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 40 AND 44 THEN '10 OCT'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 45 AND 48 THEN '11 NOV'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 49 AND 52 THEN '12 DIC'
                    END AS month,
                    deal_id,
                    pipeline,
                    COUNT(*) AS total_tasks,
                    SUM(CASE WHEN deadline_at >= ended_at AND ended_at IS NOT NULL THEN 1 ELSE 0 END) AS completed,
                    SUM(CASE WHEN ended_at > deadline_at AND ended_at IS NOT NULL THEN 1 ELSE 0 END) AS late_closed,
                    SUM(CASE WHEN CURRENT_DATE > deadline_at::date AND ended_at IS NULL THEN 1 ELSE 0 END) AS late_open,
                    SUM(CASE WHEN CURRENT_DATE <= deadline_at::date AND ended_at IS NULL THEN 1 ELSE 0 END) AS pending
                FROM activity_base
                WHERE created_at IS NOT NULL AND EXTRACT(YEAR FROM created_at)::int = @year
                GROUP BY 1, deal_id, pipeline
            )
            SELECT
                month,
                COUNT(deal_id) AS total_clients,
                COALESCE(SUM(total_tasks), 0) AS total_tasks,
                COALESCE(SUM(completed), 0) AS completed,
                COALESCE(SUM(pending), 0) AS pending,
                COALESCE(SUM(late_open), 0) AS late_open,
                COALESCE(SUM(late_closed), 0) AS late_closed,
                COALESCE(SUM(late_closed), 0)::numeric / NULLIF(SUM(total_tasks), 0) AS percentage
            FROM deal_tasks
            WHERE month IS NOT NULL
            GROUP BY month
            ORDER BY month;
            """;

        var rows = new List<object>();
        long totalClients = 0;
        long totalTasks = 0;
        long totalCompleted = 0;
        long totalPending = 0;
        long totalLateOpen = 0;
        long totalLateClosed = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var clients = reader.GetInt64(1);
            var tasks = reader.GetInt64(2);
            var completed = reader.GetInt64(3);
            var pending = reader.GetInt64(4);
            var lateOpen = reader.GetInt64(5);
            var lateClosed = reader.GetInt64(6);
            totalClients += clients;
            totalTasks += tasks;
            totalCompleted += completed;
            totalPending += pending;
            totalLateOpen += lateOpen;
            totalLateClosed += lateClosed;

            rows.Add(new
            {
                month = reader.GetString(0),
                totalClients = clients,
                totalTasks = tasks,
                completed,
                pending,
                lateOpen,
                lateClosed,
                percentage = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7)
            });
        }

        return new
        {
            year,
            totalClients,
            totalTasks,
            totalCompleted,
            totalPending,
            totalLateOpen,
            totalLateClosed,
            totalPercentage = totalTasks == 0 ? 0m : (decimal)totalLateClosed / totalTasks,
            rows
        };
    }

    public static async Task<object> GetManagementLpWeeklyTasksAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH activity_base AS (
                SELECT
                    a.bitrix_id,
                    'LP Operativa' AS pipeline,
                    a.responsible_bitrix_id AS responsible_id,
                    COALESCE(NULLIF(u.full_name, ''), a.responsible_bitrix_id, 'Sin responsable') AS responsible,
                    COALESCE(
                        a.bitrix_created_at,
                        CASE WHEN payload.payload ->> 'CREATED' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'CREATED')::timestamptz END,
                        CASE WHEN payload.payload ->> 'DATE_CREATE' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'DATE_CREATE')::timestamptz END,
                        CASE WHEN payload.payload ->> 'START_TIME' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'START_TIME')::timestamptz END
                    ) AS created_at,
                    a.deadline_at,
                    CASE WHEN payload.payload ->> 'END_TIME' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'END_TIME')::timestamptz END AS ended_at
                FROM bitrix.activities a
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = a.raw_payload_id
                LEFT JOIN bitrix.users u ON u.connection_id = a.connection_id AND u.bitrix_id = a.responsible_bitrix_id
                WHERE
                    a.owner_type IN ('2', 'DEAL', 'deal')
                    AND a.responsible_bitrix_id IN ('9482', '16230', '2070', '17844', '18384')
                    AND a.type_id = '6'
            )
            SELECT
                EXTRACT(YEAR FROM created_at)::int AS year,
                CASE
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 1 AND 4 THEN '01 ENE'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 5 AND 8 THEN '02 FEB'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 9 AND 13 THEN '03 MAR'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 14 AND 17 THEN '04 ABR'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 18 AND 21 THEN '05 MAY'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 22 AND 26 THEN '06 JUN'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 27 AND 30 THEN '07 JUL'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 31 AND 35 THEN '08 AGO'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 36 AND 39 THEN '09 SEP'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 40 AND 44 THEN '10 OCT'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 45 AND 48 THEN '11 NOV'
                    WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 49 AND 52 THEN '12 DIC'
                END AS month,
                EXTRACT(WEEK FROM created_at)::int AS week_number,
                'Semana ' || LPAD(EXTRACT(WEEK FROM created_at)::int::text, 2, '0') AS week,
                responsible_id,
                responsible,
                pipeline,
                COUNT(*) AS total_tasks,
                SUM(CASE WHEN deadline_at >= ended_at AND ended_at IS NOT NULL THEN 1 ELSE 0 END) AS completed,
                SUM(CASE WHEN ended_at > deadline_at AND ended_at IS NOT NULL THEN 1 ELSE 0 END) AS late_closed,
                SUM(CASE WHEN CURRENT_DATE >= deadline_at::date AND ended_at IS NULL THEN 1 ELSE 0 END) AS late_open,
                SUM(CASE WHEN CURRENT_DATE <= deadline_at::date AND ended_at IS NULL THEN 1 ELSE 0 END) AS pending,
                SUM(CASE WHEN ended_at > deadline_at AND ended_at IS NOT NULL THEN 1 ELSE 0 END)::numeric / NULLIF(COUNT(*), 0) AS percentage
            FROM activity_base
            WHERE created_at IS NOT NULL AND EXTRACT(YEAR FROM created_at)::int = @year
            GROUP BY 1, 2, 3, 4, 5, 6, 7
            ORDER BY month, week_number, responsible;
            """;

        var rows = new List<object>();
        long totalTasks = 0;
        long totalCompleted = 0;
        long totalPending = 0;
        long totalLateOpen = 0;
        long totalLateClosed = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var tasks = reader.GetInt64(7);
            var completed = reader.GetInt64(8);
            var lateClosed = reader.GetInt64(9);
            var lateOpen = reader.GetInt64(10);
            var pending = reader.GetInt64(11);
            totalTasks += tasks;
            totalCompleted += completed;
            totalLateClosed += lateClosed;
            totalLateOpen += lateOpen;
            totalPending += pending;

            rows.Add(new
            {
                year = reader.GetInt32(0),
                month = reader.GetString(1),
                weekNumber = reader.GetInt32(2),
                week = reader.GetString(3),
                responsibleId = reader.GetString(4),
                responsible = reader.GetString(5),
                pipeline = reader.GetString(6),
                totalTasks = tasks,
                completed,
                lateClosed,
                lateOpen,
                pending,
                percentage = reader.IsDBNull(12) ? 0m : reader.GetDecimal(12)
            });
        }

        return new
        {
            year,
            totalTasks,
            totalCompleted,
            totalPending,
            totalLateOpen,
            totalLateClosed,
            totalPercentage = totalTasks == 0 ? 0m : (decimal)totalLateClosed / totalTasks,
            rows
        };
    }

    public static async Task<object> GetManagementLpSpecialMonthlyTasksAsync(
        int year,
        string responsibleBitrixId,
        string pipelineName,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH activity_base AS (
                SELECT
                    d.bitrix_id AS deal_id,
                    p.name AS pipeline,
                    COALESCE(NULLIF(u.full_name, ''), a.responsible_bitrix_id, 'Sin responsable') AS responsible,
                    COALESCE(
                        a.bitrix_created_at,
                        CASE WHEN payload.payload ->> 'CREATED' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'CREATED')::timestamptz END,
                        CASE WHEN payload.payload ->> 'DATE_CREATE' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'DATE_CREATE')::timestamptz END,
                        CASE WHEN payload.payload ->> 'START_TIME' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'START_TIME')::timestamptz END
                    ) AS created_at,
                    a.deadline_at,
                    CASE WHEN payload.payload ->> 'END_TIME' ~ '^\d{4}-\d{2}-\d{2}' THEN (payload.payload ->> 'END_TIME')::timestamptz END AS ended_at
                FROM bitrix.activities a
                INNER JOIN bitrix.deals d ON d.connection_id = a.connection_id AND d.bitrix_id = a.owner_bitrix_id
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = a.raw_payload_id
                LEFT JOIN bitrix.users u ON u.connection_id = a.connection_id AND u.bitrix_id = a.responsible_bitrix_id
                WHERE
                    a.owner_type IN ('2', 'DEAL', 'deal')
                    AND a.responsible_bitrix_id = @responsibleBitrixId
                    AND a.type_id = '6'
                    AND p.name = @pipelineName
            ),
            deal_tasks AS (
                SELECT
                    CASE
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 1 AND 4 THEN '01 ENE'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 5 AND 8 THEN '02 FEB'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 9 AND 13 THEN '03 MAR'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 14 AND 17 THEN '04 ABR'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 18 AND 21 THEN '05 MAY'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 22 AND 26 THEN '06 JUN'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 27 AND 30 THEN '07 JUL'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 31 AND 35 THEN '08 AGO'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 36 AND 39 THEN '09 SEP'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 40 AND 44 THEN '10 OCT'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 45 AND 48 THEN '11 NOV'
                        WHEN EXTRACT(WEEK FROM created_at)::int BETWEEN 49 AND 52 THEN '12 DIC'
                    END AS month,
                    responsible,
                    pipeline,
                    deal_id,
                    COUNT(*) AS total_tasks,
                    SUM(CASE WHEN deadline_at >= ended_at AND ended_at IS NOT NULL THEN 1 ELSE 0 END) AS completed,
                    SUM(CASE WHEN ended_at > deadline_at AND ended_at IS NOT NULL THEN 1 ELSE 0 END) AS late_closed,
                    SUM(CASE WHEN CURRENT_DATE > deadline_at::date AND ended_at IS NULL THEN 1 ELSE 0 END) AS late_open,
                    SUM(CASE WHEN CURRENT_DATE <= deadline_at::date AND ended_at IS NULL THEN 1 ELSE 0 END) AS pending
                FROM activity_base
                WHERE created_at IS NOT NULL AND EXTRACT(YEAR FROM created_at)::int = @year
                GROUP BY 1, responsible, pipeline, deal_id
            )
            SELECT
                month,
                responsible,
                pipeline,
                COUNT(deal_id) AS total_clients,
                COALESCE(SUM(total_tasks), 0) AS total_tasks,
                COALESCE(SUM(completed), 0) AS completed,
                COALESCE(SUM(pending), 0) AS pending,
                COALESCE(SUM(late_open), 0) AS late_open,
                COALESCE(SUM(late_closed), 0) AS late_closed,
                COALESCE(SUM(late_closed), 0)::numeric / NULLIF(SUM(total_tasks), 0) AS percentage
            FROM deal_tasks
            WHERE month IS NOT NULL
            GROUP BY month, responsible, pipeline
            ORDER BY month, responsible, pipeline;
            """;

        var rows = new List<object>();
        long totalClients = 0;
        long totalTasks = 0;
        long totalCompleted = 0;
        long totalPending = 0;
        long totalLateOpen = 0;
        long totalLateClosed = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year);
        command.Parameters.AddWithValue("responsibleBitrixId", responsibleBitrixId);
        command.Parameters.AddWithValue("pipelineName", pipelineName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var clients = reader.GetInt64(3);
            var tasks = reader.GetInt64(4);
            var completed = reader.GetInt64(5);
            var pending = reader.GetInt64(6);
            var lateOpen = reader.GetInt64(7);
            var lateClosed = reader.GetInt64(8);
            totalClients += clients;
            totalTasks += tasks;
            totalCompleted += completed;
            totalPending += pending;
            totalLateOpen += lateOpen;
            totalLateClosed += lateClosed;

            rows.Add(new
            {
                month = reader.GetString(0),
                responsible = reader.GetString(1),
                pipeline = reader.GetString(2),
                totalClients = clients,
                totalTasks = tasks,
                completed,
                pending,
                lateOpen,
                lateClosed,
                percentage = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9)
            });
        }

        return new
        {
            year,
            totalClients,
            totalTasks,
            totalCompleted,
            totalPending,
            totalLateOpen,
            totalLateClosed,
            totalPercentage = totalTasks == 0 ? 0m : (decimal)totalLateClosed / totalTasks,
            rows
        };
    }

    public static async Task<object> GetManagementInsEmbargosDetailAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.title AS name,
                COALESCE(stage.name, d.stage_id) AS stage,
                COALESCE(NULLIF(u.full_name, ''), d.assigned_by_bitrix_id, 'Sin responsable') AS responsible,
                COALESCE(d.opportunity, 0) AS total,
                CASE
                    WHEN COALESCE(stage.name, d.stage_id) = 'MEMORIAL A JUZGADOS' THEN CURRENT_DATE - (
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529435', payload.payload ->> 'UF_CRM_1614529435') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529435', payload.payload ->> 'UF_CRM_1614529435') FROM 1 FOR 10)::date
                        END
                    ) - 90
                    WHEN COALESCE(stage.name, d.stage_id) = 'TUTELA' THEN CURRENT_DATE - (
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529916', payload.payload ->> 'UF_CRM_1614529916') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529916', payload.payload ->> 'UF_CRM_1614529916') FROM 1 FOR 10)::date
                        END
                    ) - 10
                END AS days_out_of_management
            FROM bitrix.deals d
            INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            INNER JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
            LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
            LEFT JOIN bitrix.pipeline_stages stage
                ON stage.pipeline_id = p.id
                AND stage.bitrix_stage_id = d.stage_id
            LEFT JOIN bitrix.users u
                ON u.connection_id = d.connection_id
                AND u.bitrix_id = d.assigned_by_bitrix_id
            WHERE p.category_id = 109
            ORDER BY d.title;
            """;

        return await GetManagementInsDetailRowsAsync(sql, dataSource, cancellationToken);
    }

    public static async Task<object> GetManagementInsLibranzaDetailAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                d.title AS name,
                COALESCE(stage.name, d.stage_id) AS stage,
                COALESCE(NULLIF(u.full_name, ''), d.assigned_by_bitrix_id, 'Sin responsable') AS responsible,
                COALESCE(d.opportunity, 0) AS total,
                CASE
                    WHEN COALESCE(stage.name, d.stage_id) = 'DERECHO DE PETICION' THEN CURRENT_DATE - (
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529435', payload.payload ->> 'UF_CRM_1614529435') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529435', payload.payload ->> 'UF_CRM_1614529435') FROM 1 FOR 10)::date
                        END
                    ) - 60
                    WHEN COALESCE(stage.name, d.stage_id) = 'SUPERFINANCIERA' THEN CURRENT_DATE - (
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529708', payload.payload ->> 'UF_CRM_1614529708') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529708', payload.payload ->> 'UF_CRM_1614529708') FROM 1 FOR 10)::date
                        END
                    ) - 60
                    WHEN COALESCE(stage.name, d.stage_id) = 'TUTELA' THEN CURRENT_DATE - (
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529916', payload.payload ->> 'UF_CRM_1614529916') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529916', payload.payload ->> 'UF_CRM_1614529916') FROM 1 FOR 10)::date
                        END
                    ) - 10
                    WHEN COALESCE(stage.name, d.stage_id) = 'INCIDENTE DE DESACATO' THEN CURRENT_DATE - (
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614530032', payload.payload ->> 'UF_CRM_1614530032') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614530032', payload.payload ->> 'UF_CRM_1614530032') FROM 1 FOR 10)::date
                        END
                    ) - 10
                END AS days_out_of_management
            FROM bitrix.deals d
            INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            INNER JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
            LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
            LEFT JOIN bitrix.pipeline_stages stage
                ON stage.pipeline_id = p.id
                AND stage.bitrix_stage_id = d.stage_id
            LEFT JOIN bitrix.users u
                ON u.connection_id = d.connection_id
                AND u.bitrix_id = d.assigned_by_bitrix_id
            WHERE p.category_id = 107
            ORDER BY d.title;
            """;

        return await GetManagementInsDetailRowsAsync(sql, dataSource, cancellationToken);
    }

    public static async Task<object> GetManagementInsuranceComplianceAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH metas_llamadas AS (
                SELECT
                    EXTRACT(MONTH FROM meta_date)::int AS month_number,
                    EXTRACT(YEAR FROM meta_date)::int AS year,
                    MAX(meta_value) AS call_target
                FROM (
                    SELECT
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') FROM 1 FOR 10)::date
                        END AS meta_date,
                        NULLIF(regexp_replace(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737855366', payload.payload ->> 'UF_CRM_1746737855366', ''), '[^0-9\.-]', '', 'g'), '')::numeric AS meta_value
                    FROM bitrix.deals d
                    INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                    INNER JOIN bitrix.entity_snapshots snapshot
                        ON snapshot.connection_id = d.connection_id
                        AND snapshot.entity_type = 'deal'
                        AND snapshot.bitrix_id = d.bitrix_id
                        AND snapshot.is_deleted = false
                    LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                    LEFT JOIN bitrix.pipeline_stages stage
                        ON stage.pipeline_id = p.id
                        AND stage.bitrix_stage_id = d.stage_id
                    WHERE p.category_id = 224
                        AND COALESCE(stage.name, d.stage_id) = 'Meta llamadas seguros'
                ) source
                WHERE meta_date IS NOT NULL
                GROUP BY 1, 2
            ),
            llamadas_por_mes AS (
                SELECT
                    EXTRACT(MONTH FROM a.bitrix_created_at)::int AS month_number,
                    EXTRACT(YEAR FROM a.bitrix_created_at)::int AS year,
                    COUNT(DISTINCT a.id) FILTER (
                        WHERE COALESCE(
                            activity_payload.payload #>> '{COMMUNICATIONS,0,CALL_STATUS_CODE}',
                            activity_payload.payload ->> 'CALL_STATUS_CODE_ID',
                            activity_payload.payload ->> 'CALL_STATUS_CODE'
                        ) = '200'
                        OR (
                            activity_payload.payload ->> 'START_TIME' IS NOT NULL
                            AND activity_payload.payload ->> 'END_TIME' IS NOT NULL
                            AND (activity_payload.payload ->> 'END_TIME')::timestamptz > (activity_payload.payload ->> 'START_TIME')::timestamptz
                        )
                    ) AS attended_calls
                FROM bitrix.activities a
                INNER JOIN bitrix.deals d
                    ON d.connection_id = a.connection_id
                    AND d.bitrix_id = a.owner_bitrix_id
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                LEFT JOIN bitrix.raw_payloads activity_payload ON activity_payload.id = a.raw_payload_id
                WHERE a.owner_type = '2'
                    AND a.type_id = '2'
                    AND a.responsible_bitrix_id = '17890'
                    AND p.category_id = 256
                    AND a.bitrix_created_at IS NOT NULL
                GROUP BY 1, 2
            ),
            metas_ventas AS (
                SELECT
                    EXTRACT(MONTH FROM meta_date)::int AS month_number,
                    EXTRACT(YEAR FROM meta_date)::int AS year,
                    MAX(d.opportunity) AS sales_target
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                CROSS JOIN LATERAL (
                    SELECT CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') FROM 1 FOR 10)::date
                    END AS meta_date
                ) parsed
                WHERE p.category_id = 224
                    AND COALESCE(stage.name, d.stage_id) = 'Meta comercial seguros'
                    AND meta_date IS NOT NULL
                GROUP BY 1, 2
            ),
            ventas_por_mes AS (
                SELECT
                    EXTRACT(MONTH FROM sale_date)::int AS month_number,
                    EXTRACT(YEAR FROM sale_date)::int AS year,
                    SUM(d.opportunity) AS sales_amount
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                CROSS JOIN LATERAL (
                    SELECT CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1651008361471', payload.payload ->> 'UF_CRM_1651008361471') FROM 1 FOR 10)::date
                    END AS sale_date
                ) parsed
                WHERE p.category_id = 278
                    AND sale_date IS NOT NULL
                GROUP BY 1, 2
            ),
            months AS (
                SELECT month_number, year FROM llamadas_por_mes
                UNION
                SELECT month_number, year FROM ventas_por_mes
                UNION
                SELECT month_number, year FROM metas_llamadas
                UNION
                SELECT month_number, year FROM metas_ventas
            ),
            result AS (
                SELECT
                    months.year,
                    months.month_number,
                    LPAD(months.month_number::text, 2, '0') || ' ' ||
                        CASE months.month_number
                            WHEN 1 THEN 'ENE' WHEN 2 THEN 'FEB' WHEN 3 THEN 'MAR'
                            WHEN 4 THEN 'ABR' WHEN 5 THEN 'MAY' WHEN 6 THEN 'JUN'
                            WHEN 7 THEN 'JUL' WHEN 8 THEN 'AGO' WHEN 9 THEN 'SEP'
                            WHEN 10 THEN 'OCT' WHEN 11 THEN 'NOV' WHEN 12 THEN 'DIC'
                        END AS month,
                    COALESCE(llamadas_por_mes.attended_calls, 0) AS attended_calls,
                    COALESCE(metas_llamadas.call_target, 0) AS call_target,
                    COALESCE(ventas_por_mes.sales_amount, 0) AS sales_amount,
                    COALESCE(metas_ventas.sales_target, 0) AS sales_target,
                    ROUND(COALESCE(llamadas_por_mes.attended_calls, 0)::numeric / NULLIF(metas_llamadas.call_target, 0), 2) AS call_compliance,
                    ROUND(COALESCE(ventas_por_mes.sales_amount, 0) / NULLIF(metas_ventas.sales_target, 0), 2) AS sales_compliance,
                    ROUND(
                        (COALESCE(llamadas_por_mes.attended_calls, 0)::numeric / NULLIF(metas_llamadas.call_target, 0) * 0.2) +
                        (COALESCE(ventas_por_mes.sales_amount, 0) / NULLIF(metas_ventas.sales_target, 0) * 0.8),
                        2
                    ) AS total_compliance
                FROM months
                LEFT JOIN llamadas_por_mes
                    ON llamadas_por_mes.month_number = months.month_number
                    AND llamadas_por_mes.year = months.year
                LEFT JOIN metas_llamadas
                    ON metas_llamadas.month_number = months.month_number
                    AND metas_llamadas.year = months.year
                LEFT JOIN ventas_por_mes
                    ON ventas_por_mes.month_number = months.month_number
                    AND ventas_por_mes.year = months.year
                LEFT JOIN metas_ventas
                    ON metas_ventas.month_number = months.month_number
                    AND metas_ventas.year = months.year
            )
            SELECT
                year,
                month_number,
                month,
                call_compliance,
                sales_compliance,
                total_compliance,
                sales_target,
                sales_amount,
                sales_compliance AS commercial_compliance
            FROM result
            ORDER BY year, month_number;
            """;

        var kpiRows = new List<object>();
        var commercialRows = new List<object>();
        decimal totalCompliance = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var total = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5);
            if (total.HasValue)
            {
                totalCompliance += total.Value;
            }

            var year = reader.GetInt32(0);
            var monthNumber = reader.GetInt32(1);
            var month = reader.GetString(2);
            var callCompliance = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
            var salesCompliance = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4);
            var salesTarget = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6);
            var salesAmount = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7);
            var commercialCompliance = reader.IsDBNull(8) ? (decimal?)null : reader.GetDecimal(8);

            kpiRows.Add(new
            {
                year,
                monthNumber,
                month,
                callCompliance,
                salesCompliance,
                totalCompliance = total
            });

            commercialRows.Add(new
            {
                year,
                monthNumber,
                month,
                monthlyTarget = salesTarget,
                totalSales = salesAmount,
                compliance = commercialCompliance
            });
        }

        return new
        {
            compliance = totalCompliance,
            kpiRows,
            commercialRows
        };
    }

    public static async Task<object> GetManagementInsuranceMonthlyOperationsAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string callsSql = """
            WITH metas_mensuales AS (
                SELECT
                    EXTRACT(MONTH FROM meta_date)::int AS month_number,
                    EXTRACT(YEAR FROM meta_date)::int AS year,
                    MAX(meta_value) AS monthly_target
                FROM (
                    SELECT
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') FROM 1 FOR 10)::date
                        END AS meta_date,
                        NULLIF(regexp_replace(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737855366', payload.payload ->> 'UF_CRM_1746737855366', ''), '[^0-9\.-]', '', 'g'), '')::numeric AS meta_value
                    FROM bitrix.deals d
                    INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                    INNER JOIN bitrix.entity_snapshots snapshot
                        ON snapshot.connection_id = d.connection_id
                        AND snapshot.entity_type = 'deal'
                        AND snapshot.bitrix_id = d.bitrix_id
                        AND snapshot.is_deleted = false
                    LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                    LEFT JOIN bitrix.pipeline_stages stage
                        ON stage.pipeline_id = p.id
                        AND stage.bitrix_stage_id = d.stage_id
                    WHERE p.category_id = 224
                        AND COALESCE(stage.name, d.stage_id) = 'Meta llamadas seguros'
                ) source
                WHERE meta_date IS NOT NULL
                GROUP BY 1, 2
            ),
            llamadas_por_mes AS (
                SELECT
                    EXTRACT(MONTH FROM a.bitrix_created_at)::int AS month_number,
                    EXTRACT(YEAR FROM a.bitrix_created_at)::int AS year,
                    COUNT(DISTINCT a.id) AS outgoing_calls,
                    COUNT(DISTINCT a.id) FILTER (
                        WHERE COALESCE(
                            activity_payload.payload #>> '{COMMUNICATIONS,0,CALL_STATUS_CODE}',
                            activity_payload.payload ->> 'CALL_STATUS_CODE_ID',
                            activity_payload.payload ->> 'CALL_STATUS_CODE'
                        ) = '200'
                        OR (
                            activity_payload.payload ->> 'START_TIME' IS NOT NULL
                            AND activity_payload.payload ->> 'END_TIME' IS NOT NULL
                            AND (activity_payload.payload ->> 'END_TIME')::timestamptz > (activity_payload.payload ->> 'START_TIME')::timestamptz
                        )
                    ) AS effective_calls
                FROM bitrix.activities a
                INNER JOIN bitrix.deals d
                    ON d.connection_id = a.connection_id
                    AND d.bitrix_id = a.owner_bitrix_id
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                LEFT JOIN bitrix.raw_payloads activity_payload ON activity_payload.id = a.raw_payload_id
                WHERE a.owner_type = '2'
                    AND a.type_id = '2'
                    AND a.responsible_bitrix_id = '17890'
                    AND p.category_id = 256
                    AND a.bitrix_created_at IS NOT NULL
                    AND EXTRACT(YEAR FROM d.bitrix_created_at) = 2025
                GROUP BY 1, 2
            )
            SELECT
                l.year,
                l.month_number,
                LPAD(l.month_number::text, 2, '0') || ' ' ||
                    CASE l.month_number
                        WHEN 1 THEN 'ENE' WHEN 2 THEN 'FEB' WHEN 3 THEN 'MAR'
                        WHEN 4 THEN 'ABR' WHEN 5 THEN 'MAY' WHEN 6 THEN 'JUN'
                        WHEN 7 THEN 'JUL' WHEN 8 THEN 'AGO' WHEN 9 THEN 'SEP'
                        WHEN 10 THEN 'OCT' WHEN 11 THEN 'NOV' WHEN 12 THEN 'DIC'
                    END AS month,
                COALESCE(m.monthly_target, 0) AS monthly_target,
                COALESCE(l.outgoing_calls, 0) AS outgoing_calls,
                COALESCE(l.effective_calls, 0) AS effective_calls,
                COALESCE(l.outgoing_calls, 0) - COALESCE(l.effective_calls, 0) AS rejected_calls,
                CASE
                    WHEN COALESCE(m.monthly_target, 0) = 0 THEN 0
                    ELSE ROUND(COALESCE(l.effective_calls, 0)::numeric / NULLIF(m.monthly_target, 0), 2)
                END AS compliance
            FROM llamadas_por_mes l
            LEFT JOIN metas_mensuales m
                ON m.month_number = l.month_number
                AND m.year = l.year
            ORDER BY l.year, l.month_number;
            """;

        const string quotesSql = """
            WITH metas_mensuales AS (
                SELECT
                    EXTRACT(MONTH FROM meta_date)::int AS month_number,
                    EXTRACT(YEAR FROM meta_date)::int AS year,
                    MAX(meta_value) AS monthly_target,
                    MAX(business_days) AS business_days
                FROM (
                    SELECT
                        CASE
                            WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') ~ '^\d{4}-\d{2}-\d{2}'
                                THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') FROM 1 FOR 10)::date
                        END AS meta_date,
                        NULLIF(regexp_replace(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737855366', payload.payload ->> 'UF_CRM_1746737855366', ''), '[^0-9\.-]', '', 'g'), '')::numeric AS meta_value,
                        NULLIF(regexp_replace(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1747243052183', payload.payload ->> 'UF_CRM_1747243052183', ''), '[^0-9\.-]', '', 'g'), '')::int AS business_days
                    FROM bitrix.deals d
                    INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                    INNER JOIN bitrix.entity_snapshots snapshot
                        ON snapshot.connection_id = d.connection_id
                        AND snapshot.entity_type = 'deal'
                        AND snapshot.bitrix_id = d.bitrix_id
                        AND snapshot.is_deleted = false
                    LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                    LEFT JOIN bitrix.pipeline_stages stage
                        ON stage.pipeline_id = p.id
                        AND stage.bitrix_stage_id = d.stage_id
                    WHERE p.category_id = 224
                        AND COALESCE(stage.name, d.stage_id) = 'Meta cotizaciones seguros'
                ) source
                WHERE meta_date IS NOT NULL
                GROUP BY 1, 2
            ),
            polizas_por_mes AS (
                SELECT
                    EXTRACT(MONTH FROM quote_date)::int AS month_number,
                    EXTRACT(YEAR FROM quote_date)::int AS year,
                    COUNT(*) FILTER (
                        WHERE NULLIF(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746189764', payload.payload ->> 'UF_CRM_1746189764', ''), '') IS NOT NULL
                    ) AS generated_quotes
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                CROSS JOIN LATERAL (
                    SELECT CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803') FROM 1 FOR 10)::date
                    END AS quote_date
                ) parsed
                WHERE p.category_id = 256
                    AND quote_date IS NOT NULL
                GROUP BY 1, 2
            )
            SELECT
                p.year,
                p.month_number,
                LPAD(p.month_number::text, 2, '0') || ' ' ||
                    CASE p.month_number
                        WHEN 1 THEN 'ENE' WHEN 2 THEN 'FEB' WHEN 3 THEN 'MAR'
                        WHEN 4 THEN 'ABR' WHEN 5 THEN 'MAY' WHEN 6 THEN 'JUN'
                        WHEN 7 THEN 'JUL' WHEN 8 THEN 'AGO' WHEN 9 THEN 'SEP'
                        WHEN 10 THEN 'OCT' WHEN 11 THEN 'NOV' WHEN 12 THEN 'DIC'
                    END AS month,
                COALESCE(m.monthly_target, 0) AS monthly_target,
                COALESCE(p.generated_quotes, 0) AS generated_quotes,
                CASE
                    WHEN COALESCE(m.monthly_target, 0) = 0 THEN 0
                    ELSE ROUND(COALESCE(p.generated_quotes, 0)::numeric / NULLIF(m.monthly_target, 0), 2)
                END AS compliance,
                m.business_days
            FROM polizas_por_mes p
            LEFT JOIN metas_mensuales m
                ON m.month_number = p.month_number
                AND m.year = p.year
            ORDER BY p.year, p.month_number;
            """;

        var callRows = new List<object>();
        var quoteRows = new List<object>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(callsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                callRows.Add(new
                {
                    year = reader.GetInt32(0),
                    monthNumber = reader.GetInt32(1),
                    month = reader.GetString(2),
                    monthlyTarget = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                    outgoingCalls = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    effectiveCalls = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                    rejectedCalls = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                    compliance = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7)
                });
            }
        }

        await using (var command = new NpgsqlCommand(quotesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                quoteRows.Add(new
                {
                    year = reader.GetInt32(0),
                    monthNumber = reader.GetInt32(1),
                    month = reader.GetString(2),
                    monthlyTarget = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                    generatedQuotes = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    compliance = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                    businessDays = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6)
                });
            }
        }

        return new { callRows, quoteRows };
    }

    public static async Task<object> GetManagementInsuranceOutOfTimeAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string baseSql = """
            WITH deal_dates AS (
                SELECT
                    d.bitrix_id AS id,
                    d.title AS name,
                    COALESCE(stage.name, d.stage_id) AS stage,
                    CASE COALESCE(stage.name, d.stage_id)
                        WHEN 'NUEVO PROSPECTO' THEN 2
                        WHEN 'CONTACTADO EN SEGUIMIENTO' THEN 3
                        WHEN 'RECOPILANDO INFORMACIÓN' THEN 5
                        WHEN 'SOLICITUD COTIZACIÓN' THEN 3
                        WHEN 'COTIZACIÓN CON NOVEDAD' THEN 3
                        WHEN 'COTIZACIÓN GENERADA' THEN 3
                        WHEN 'PROGRAMACIÓN SUSTENTACIÓN' THEN 8
                        WHEN 'SOLICITUD EMISIÓN PÓLIZA' THEN 3
                        WHEN 'NOVEDAD DE EMISIÓN' THEN 8
                        WHEN 'PÓLIZA EMITIDA' THEN 3
                        WHEN 'NUEVOS ENDOSOS' THEN 3
                        WHEN 'ENDOSO RADICADO' THEN 3
                        WHEN 'SEGUIMIENTO A APROBACIÓN' THEN 3
                        WHEN 'SEGUIMIENTO RENOVACIONES' THEN 334
                    END AS limit_days,
                    CASE COALESCE(stage.name, d.stage_id)
                        WHEN 'NUEVO PROSPECTO' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1623281084711', payload.payload ->> 'UF_CRM_1623281084711')
                        WHEN 'CONTACTADO EN SEGUIMIENTO' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1601670888849', payload.payload ->> 'UF_CRM_1601670888849')
                        WHEN 'RECOPILANDO INFORMACIÓN' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676487033939', payload.payload ->> 'UF_CRM_1676487033939')
                        WHEN 'SOLICITUD COTIZACIÓN' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1601671184915', payload.payload ->> 'UF_CRM_1601671184915')
                        WHEN 'COTIZACIÓN CON NOVEDAD' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676486744268', payload.payload ->> 'UF_CRM_1676486744268')
                        WHEN 'COTIZACIÓN GENERADA' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1746737634803', payload.payload ->> 'UF_CRM_1746737634803')
                        WHEN 'PROGRAMACIÓN SUSTENTACIÓN' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676486694789', payload.payload ->> 'UF_CRM_1676486694789')
                        WHEN 'SOLICITUD EMISIÓN PÓLIZA' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1601671494647', payload.payload ->> 'UF_CRM_1601671494647')
                        WHEN 'NOVEDAD DE EMISIÓN' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1616543806805', payload.payload ->> 'UF_CRM_1616543806805')
                        WHEN 'PÓLIZA EMITIDA' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1601671936104', payload.payload ->> 'UF_CRM_1601671936104')
                        WHEN 'NUEVOS ENDOSOS' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1616543676428', payload.payload ->> 'UF_CRM_1616543676428')
                        WHEN 'ENDOSO RADICADO' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1601672168191', payload.payload ->> 'UF_CRM_1601672168191')
                        WHEN 'SEGUIMIENTO A APROBACIÓN' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529675', payload.payload ->> 'UF_CRM_1614529675')
                        WHEN 'SEGUIMIENTO RENOVACIONES' THEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1614529793', payload.payload ->> 'UF_CRM_1614529793')
                    END AS raw_date
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id IN (256, 278)
            ),
            calculated AS (
                SELECT
                    id,
                    name,
                    stage,
                    CASE
                        WHEN raw_date ~ '^\d{4}-\d{2}-\d{2}' THEN (CURRENT_DATE - SUBSTRING(raw_date FROM 1 FOR 10)::date)
                    END AS elapsed_days,
                    limit_days
                FROM deal_dates
            )
            """;

        var summaryRows = new List<object>();
        var detailRows = new List<object>();
        long totalNegotiations = 0;
        long totalOutOfTime = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(baseSql + """
            SELECT
                stage,
                COUNT(*) AS total_negotiations,
                COUNT(*) FILTER (WHERE elapsed_days > limit_days) AS out_of_time
            FROM calculated
            GROUP BY stage
            ORDER BY stage;
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var total = reader.GetInt64(1);
                var outOfTime = reader.GetInt64(2);
                totalNegotiations += total;
                totalOutOfTime += outOfTime;

                summaryRows.Add(new
                {
                    stage = reader.GetString(0),
                    totalNegotiations = total,
                    outOfTime
                });
            }
        }

        await using (var command = new NpgsqlCommand(baseSql + """
            SELECT
                id,
                name,
                stage,
                1 AS days_out_of_management
            FROM calculated
            WHERE elapsed_days > limit_days
            ORDER BY stage, name;
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                detailRows.Add(new
                {
                    id = reader.GetString(0),
                    name = reader.GetString(1),
                    stage = reader.GetString(2),
                    daysOutOfManagement = reader.GetInt32(3)
                });
            }
        }

        return new
        {
            summaryRows,
            detailRows,
            totals = new
            {
                totalNegotiations,
                outOfTime = totalOutOfTime
            }
        };
    }

    public static async Task<object> GetManagementCustomerServiceSummaryAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH pqrfs AS (
                SELECT
                    d.id,
                    d.bitrix_created_at::date AS created_date,
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1694637524493', payload.payload ->> 'UF_CRM_1694637524493') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1694637524493', payload.payload ->> 'UF_CRM_1694637524493') FROM 1 FOR 10)::date
                    END AS response_date,
                    CASE COALESCE(snapshot.custom_fields ->> 'UF_CRM_1607022885798', payload.payload ->> 'UF_CRM_1607022885798', '')
                        WHEN '6541' THEN 'PETICIÓN'
                        WHEN '6543' THEN 'QUEJA'
                        WHEN '6545' THEN 'RECLAMO'
                        WHEN '6549' THEN 'FELICITACIÓN'
                        WHEN '37486' THEN 'SUGERENCIA'
                        ELSE UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1607022885798', payload.payload ->> 'UF_CRM_1607022885798', ''))
                    END AS request_type,
                    COALESCE(stage.name, d.stage_id) AS stage
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id = 97
                    AND EXTRACT(YEAR FROM d.bitrix_created_at) = @year
                    AND COALESCE(stage.name, d.stage_id) IN (
                        'PQRF RADICADOS',
                        'ESCALADO',
                        'ESCALADO EN GESTIÓN',
                        'GESTIONANDO',
                        'ESCALADO VENCIDO',
                        'ESCALADO CON RESPUESTA',
                        'REASIGNACIÓN RESPONSABLE',
                        'RESPUESTA PROYECTADA',
                        'PQRF  FINALIZADO'
                    )
            )
            SELECT
                COUNT(*) AS received,
                COUNT(*) FILTER (
                    WHERE request_type = 'PETICIÓN'
                        AND response_date = created_date + 3
                ) + COUNT(*) FILTER (
                    WHERE request_type = 'QUEJA'
                        AND response_date = created_date + 6
                ) + COUNT(*) FILTER (
                    WHERE request_type = 'RECLAMO'
                        AND response_date = created_date + 6
                ) AS on_time,
                (
                    (
                        COUNT(*) FILTER (
                            WHERE request_type = 'PETICIÓN'
                                AND response_date = created_date + 3
                        ) + COUNT(*) FILTER (
                            WHERE request_type = 'QUEJA'
                                AND response_date = created_date + 6
                        ) + COUNT(*) FILTER (
                            WHERE request_type = 'RECLAMO'
                                AND response_date = created_date + 6
                        )
                    )::numeric / NULLIF(COUNT(*), 0)
                ) AS compliance
            FROM pqrfs;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new { year, compliance = 0m, received = 0L, onTime = 0L };
        }

        return new
        {
            year,
            received = reader.IsDBNull(0) ? 0L : reader.GetInt64(0),
            onTime = reader.IsDBNull(1) ? 0L : reader.GetInt64(1),
            compliance = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2)
        };
    }

    public static async Task<object> GetManagementCustomerServiceChartsAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH pqrfs AS (
                SELECT
                    EXTRACT(MONTH FROM d.bitrix_created_at)::int AS month_number,
                    CASE EXTRACT(MONTH FROM d.bitrix_created_at)::int
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                    END AS month,
                    CASE COALESCE(snapshot.custom_fields ->> 'UF_CRM_1607022885798', payload.payload ->> 'UF_CRM_1607022885798', '')
                        WHEN '6541' THEN 'PETICIÓN'
                        WHEN '6543' THEN 'QUEJA'
                        WHEN '6545' THEN 'RECLAMO'
                        WHEN '6549' THEN 'FELICITACIÓN'
                        WHEN '37486' THEN 'SUGERENCIA'
                        ELSE COALESCE(NULLIF(UPPER(snapshot.custom_fields ->> 'UF_CRM_1607022885798'), ''), 'SIN DEFINIR')
                    END AS requirement,
                    COALESCE(stage.name, d.stage_id) AS stage
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id = 97
                    AND EXTRACT(YEAR FROM d.bitrix_created_at) = @year
                    AND COALESCE(stage.name, d.stage_id) IN (
                        'PQRF RADICADOS',
                        'ESCALADO',
                        'ESCALADO EN GESTIÓN',
                        'GESTIONANDO',
                        'ESCALADO VENCIDO',
                        'ESCALADO CON RESPUESTA',
                        'REASIGNACIÓN RESPONSABLE',
                        'RESPUESTA PROYECTADA',
                        'PQRF  FINALIZADO'
                    )
            ),
            requirements AS (
                SELECT requirement, month_number, month, COUNT(*) AS cases_count
                FROM pqrfs
                GROUP BY requirement, month_number, month
            ),
            monthly AS (
                SELECT month_number, month, COUNT(*) AS received
                FROM pqrfs
                GROUP BY month_number, month
            )
            SELECT
                'requirement' AS row_type,
                requirement AS label,
                month_number,
                month,
                cases_count AS value
            FROM requirements
            UNION ALL
            SELECT
                'monthly' AS row_type,
                month AS label,
                month_number,
                month,
                received AS value
            FROM monthly
            ORDER BY row_type, value DESC, month_number;
            """;

        var requirements = new List<(string Requirement, int MonthNumber, string Month, long Cases)>();
        var monthly = new List<(string Month, int MonthNumber, long Received)>();
        long totalRequirements = 0;
        long totalReceived = 0;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var rowType = reader.GetString(0);
            var label = reader.IsDBNull(1) ? "SIN DEFINIR" : reader.GetString(1);
            var monthNumber = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            var month = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var value = reader.GetInt64(4);

            if (rowType == "requirement")
            {
                totalRequirements += value;
                requirements.Add((label, monthNumber, month, value));
            }
            else
            {
                totalReceived += value;
                monthly.Add((label, monthNumber, value));
            }
        }

        return new
        {
            year,
            requirements = requirements.Select(row => new
            {
                requirement = row.Requirement,
                month = row.Month,
                monthNumber = row.MonthNumber,
                cases = row.Cases
            }),
            monthly = monthly
                .OrderBy(row => row.MonthNumber)
                .Select(row => new
                {
                    month = row.Month,
                    monthNumber = row.MonthNumber,
                    received = row.Received
                }),
            totalRequirements,
            totalReceived
        };
    }

    public static async Task<object> GetManagementCustomerServiceResponseAverageAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH response_days AS (
                SELECT
                    EXTRACT(MONTH FROM d.bitrix_created_at)::int AS month_number,
                    CASE EXTRACT(MONTH FROM d.bitrix_created_at)::int
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                    END AS month,
                    CASE COALESCE(snapshot.custom_fields ->> 'UF_CRM_1690228297166', payload.payload ->> 'UF_CRM_1690228297166', '')
                        WHEN '30488' THEN 'Felicitación'
                        WHEN '30490' THEN 'Petición'
                        WHEN '30492' THEN 'Queja'
                        WHEN '30494' THEN 'Reclamo'
                        WHEN '37490' THEN 'Sugerencia'
                        WHEN '6541' THEN 'Petición'
                        WHEN '6543' THEN 'Queja'
                        WHEN '6545' THEN 'Reclamo'
                        WHEN '6549' THEN 'Felicitación'
                        WHEN '37486' THEN 'Sugerencia'
                        ELSE COALESCE(NULLIF(snapshot.custom_fields ->> 'UF_CRM_1690228297166', ''), 'Sin definir')
                    END AS requirement,
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1694637524493', payload.payload ->> 'UF_CRM_1694637524493') ~ '^\d{4}-\d{2}-\d{2}'
                            THEN (
                                SUBSTRING(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1694637524493', payload.payload ->> 'UF_CRM_1694637524493') FROM 1 FOR 10)::date
                                - d.bitrix_created_at::date
                            )
                    END AS response_days
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                LEFT JOIN bitrix.pipeline_stages stage
                    ON stage.pipeline_id = p.id
                    AND stage.bitrix_stage_id = d.stage_id
                WHERE p.category_id = 97
                    AND EXTRACT(YEAR FROM d.bitrix_created_at) = @year
                    AND COALESCE(stage.name, d.stage_id) = 'PQRF  FINALIZADO'
            )
            SELECT
                month,
                month_number,
                requirement,
                COALESCE(ROUND(AVG(response_days)::numeric, 2), 0) AS average_days
            FROM response_days
            WHERE requirement <> 'Sin definir'
            GROUP BY month, month_number, requirement
            ORDER BY requirement, month_number;
            """;

        var rows = new List<(string Month, int MonthNumber, string Requirement, decimal Average)>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("year", year);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetDecimal(3)));
        }

        return new
        {
            year,
            rows = rows.Select(row => new
            {
                month = row.Month,
                monthNumber = row.MonthNumber,
                requirement = row.Requirement,
                average = row.Average
            })
        };
    }

    public static async Task<object> GetManagementCustomerServiceWithdrawalsAsync(
        int year,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string summarySql = """
            WITH radicado AS (
                SELECT
                    CASE
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22560' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ENERO%' THEN '01 ENE'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '22562' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%FEBRERO%' THEN '02 FEB'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39144' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MARZO%' THEN '03 MAR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39146' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%ABRIL%' THEN '04 ABR'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39148' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%MAYO%' THEN '05 MAY'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39150' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JUNIO%' THEN '06 JUN'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39152' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%JULIO%' THEN '07 JUL'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39154' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%AGOSTO%' THEN '08 AGO'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39156' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%SEPTIEMBRE%' THEN '09 SEP'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39158' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%OCTUBRE%' THEN '10 OCT'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39160' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%NOVIEMBRE%' THEN '11 NOV'
                        WHEN snapshot.custom_fields ->> 'UF_CRM_1676419915' = '39162' OR UPPER(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1676419915', '')) LIKE '%DICIEMBRE%' THEN '12 DIC'
                        ELSE '13 OTRO'
                    END AS month,
                    CASE WHEN p.category_id = 28 THEN 'Insolvencia' WHEN p.category_id = 10 THEN 'RCH' END AS line,
                    COUNT(*) AS started
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                WHERE p.category_id IN (10, 28)
                    AND (
                        snapshot.custom_fields ->> 'UF_CRM_1737653376' = @yearText
                        OR snapshot.custom_fields ->> 'UF_CRM_1737653376' = CASE @yearText
                            WHEN '2024' THEN '37206'
                            WHEN '2025' THEN '37036'
                            WHEN '2026' THEN '39138'
                        END
                    )
                GROUP BY month, line
            ),
            desistimiento AS (
                SELECT
                    EXTRACT(MONTH FROM d.bitrix_created_at - INTERVAL '8 hours')::int AS month_number,
                    CASE EXTRACT(MONTH FROM d.bitrix_created_at - INTERVAL '8 hours')::int
                        WHEN 1 THEN '01 ENE'
                        WHEN 2 THEN '02 FEB'
                        WHEN 3 THEN '03 MAR'
                        WHEN 4 THEN '04 ABR'
                        WHEN 5 THEN '05 MAY'
                        WHEN 6 THEN '06 JUN'
                        WHEN 7 THEN '07 JUL'
                        WHEN 8 THEN '08 AGO'
                        WHEN 9 THEN '09 SEP'
                        WHEN 10 THEN '10 OCT'
                        WHEN 11 THEN '11 NOV'
                        WHEN 12 THEN '12 DIC'
                        ELSE '13 OTRO'
                    END AS month,
                    CASE
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') = '30608' THEN 'Insolvencia'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') = '37916' THEN 'RCH'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') = 'Solicitud de Desistimiento del Proceso y Devolución de Pagos' THEN 'Insolvencia'
                        WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') = 'Desistimiento RCH' THEN 'RCH'
                    END AS line,
                    COUNT(*) AS withdrawn
                FROM bitrix.deals d
                INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
                INNER JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
                LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
                WHERE p.category_id = 97
                    AND EXTRACT(YEAR FROM d.bitrix_created_at) = @year
                    AND COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') IN (
                        '30608',
                        '37916',
                        'Solicitud de Desistimiento del Proceso y Devolución de Pagos',
                        'Desistimiento RCH'
                    )
                    AND COALESCE(snapshot.custom_fields ->> 'UF_CRM_1592275850', payload.payload ->> 'UF_CRM_1592275850') IN ('P', '3165')
                GROUP BY month_number, month, line
            )
            SELECT
                COALESCE(r.month, d.month) AS month,
                COALESCE(d.month_number, array_position(ARRAY['01 ENE','02 FEB','03 MAR','04 ABR','05 MAY','06 JUN','07 JUL','08 AGO','09 SEP','10 OCT','11 NOV','12 DIC'], r.month)) AS month_number,
                COALESCE(r.line, d.line) AS line,
                COALESCE(r.started, 0) AS started,
                COALESCE(d.withdrawn, 0) AS withdrawn,
                COALESCE(d.withdrawn, 0)::numeric / NULLIF(COALESCE(r.started, 0), 0) AS withdrawal_rate
            FROM radicado r
            FULL OUTER JOIN desistimiento d
                ON r.month = d.month
                AND r.line = d.line
            WHERE COALESCE(r.month, d.month) <> '13 OTRO'
              AND COALESCE(r.line, d.line) IS NOT NULL
            ORDER BY line, month_number;
            """;

        const string detailSql = """
            SELECT
                EXTRACT(MONTH FROM d.bitrix_created_at - INTERVAL '8 hours')::int AS month_number,
                CASE EXTRACT(MONTH FROM d.bitrix_created_at - INTERVAL '8 hours')::int
                    WHEN 1 THEN '01 ENE'
                    WHEN 2 THEN '02 FEB'
                    WHEN 3 THEN '03 MAR'
                    WHEN 4 THEN '04 ABR'
                    WHEN 5 THEN '05 MAY'
                    WHEN 6 THEN '06 JUN'
                    WHEN 7 THEN '07 JUL'
                    WHEN 8 THEN '08 AGO'
                    WHEN 9 THEN '09 SEP'
                    WHEN 10 THEN '10 OCT'
                    WHEN 11 THEN '11 NOV'
                    WHEN 12 THEN '12 DIC'
                END AS month,
                d.bitrix_id,
                CASE COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687')
                    WHEN '30608' THEN 'Solicitud de Desistimiento del Proceso y Devolución de Pagos'
                    WHEN '37916' THEN 'Desistimiento RCH'
                    ELSE COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687', 'no seleccionado')
                END AS reason,
                CASE COALESCE(snapshot.custom_fields ->> 'UF_CRM_1754067079', payload.payload ->> 'UF_CRM_1754067079')
                    WHEN '39954' THEN 'Cliente Ilocalizable'
                    WHEN '39956' THEN 'Cliente No Aporta Documentos'
                    WHEN '39958' THEN 'Decisión Dentro del Tiempo de la Cláusula'
                    WHEN '38476' THEN 'Decisión Voluntaria'
                    WHEN '39960' THEN 'Demoras en la Gestión Operativa'
                    WHEN '39962' THEN 'Disminución de la Capacidad de Pago / Cuota'
                    WHEN '39964' THEN 'Disminución de la Capacidad de Pago / Honorarios'
                    WHEN '38364' THEN 'Documentación Nunca Subsanada'
                    WHEN '38360' THEN 'Errores u Omisiones en la Asesoría Comercial'
                    WHEN '38366' THEN 'Falta de Seguimiento Comercial'
                    WHEN '38368' THEN 'Falta de Seguimiento Operativo'
                    WHEN '38358' THEN 'Inviabilidad del Proceso'
                    WHEN '38362' THEN 'Inexistencia del Contrato Firmado'
                    WHEN '39966' THEN 'Negociación Duplicada (Año Anterior)'
                    WHEN '39968' THEN 'Perfil Financiero o Crediticio No Viable'
                    WHEN '38370' THEN 'Radicación Pendiente'
                    ELSE COALESCE(NULLIF(snapshot.custom_fields ->> 'UF_CRM_1754067079', ''), 'no seleccionado')
                END AS definitive_reason,
                CASE
                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') IN ('30608', 'Solicitud de Desistimiento del Proceso y Devolución de Pagos') THEN 'Insolvencia'
                    WHEN COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') IN ('37916', 'Desistimiento RCH') THEN 'RCH'
                END AS line,
                COALESCE(leader.full_name, snapshot.custom_fields ->> 'UF_CRM_1611163473', payload.payload ->> 'UF_CRM_1611163473') AS process_leader,
                NULLIF(
                    REGEXP_REPLACE(
                        REPLACE(
                            REPLACE(
                                SPLIT_PART(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1584482115955', payload.payload ->> 'UF_CRM_1584482115955', ''), '|', 1),
                                '$',
                                ''
                            ),
                            ',',
                            '.'
                        ),
                        '[^0-9.-]',
                        '',
                        'g'
                    ),
                    ''
                )::numeric AS refund_value
            FROM bitrix.deals d
            INNER JOIN bitrix.pipelines p ON p.id = d.pipeline_id
            INNER JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
            LEFT JOIN bitrix.raw_payloads payload ON payload.id = d.raw_payload_id
            LEFT JOIN bitrix.users leader
                ON leader.connection_id = d.connection_id
                AND leader.bitrix_id = REGEXP_REPLACE(COALESCE(snapshot.custom_fields ->> 'UF_CRM_1611163473', payload.payload ->> 'UF_CRM_1611163473', ''), '[^0-9]', '', 'g')
            WHERE p.category_id = 97
                AND EXTRACT(YEAR FROM d.bitrix_created_at) = @year
                AND COALESCE(snapshot.custom_fields ->> 'UF_CRM_1721937687', payload.payload ->> 'UF_CRM_1721937687') IN (
                    '30608',
                    '37916',
                    'Solicitud de Desistimiento del Proceso y Devolución de Pagos',
                    'Desistimiento RCH'
                )
                AND COALESCE(snapshot.custom_fields ->> 'UF_CRM_1592275850', payload.payload ->> 'UF_CRM_1592275850') IN ('P', '3165')
            ORDER BY line, month_number, d.bitrix_id;
            """;

        var summaryRows = new List<(string Month, int MonthNumber, string Line, long Started, long Withdrawn, decimal? Rate)>();
        var detailRows = new List<(string Month, int MonthNumber, string Id, string Reason, string DefinitiveReason, string Line, string? ProcessLeader, decimal? RefundValue)>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(summarySql, connection))
        {
            command.Parameters.AddWithValue("year", year);
            command.Parameters.AddWithValue("yearText", year.ToString(CultureInfo.InvariantCulture));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                summaryRows.Add((
                    reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5)));
            }
        }

        await using (var command = new NpgsqlCommand(detailSql, connection))
        {
            command.Parameters.AddWithValue("year", year);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                detailRows.Add((
                    reader.GetString(1),
                    reader.GetInt32(0),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetDecimal(7)));
            }
        }

        static object ProjectSummary(IEnumerable<(string Month, int MonthNumber, string Line, long Started, long Withdrawn, decimal? Rate)> rows) =>
            rows.Select(row => new
            {
                month = row.Month,
                monthNumber = row.MonthNumber,
                line = row.Line,
                started = row.Started,
                withdrawn = row.Withdrawn,
                withdrawalRate = row.Rate
            }).ToArray();

        static object ProjectDetail(IEnumerable<(string Month, int MonthNumber, string Id, string Reason, string DefinitiveReason, string Line, string? ProcessLeader, decimal? RefundValue)> rows) =>
            rows.Select(row => new
            {
                month = row.Month,
                monthNumber = row.MonthNumber,
                id = row.Id,
                reason = row.Reason,
                definitiveReason = row.DefinitiveReason,
                line = row.Line,
                processLeader = row.ProcessLeader,
                refundValue = row.RefundValue
            }).ToArray();

        return new
        {
            year,
            insolvencySummary = ProjectSummary(summaryRows.Where(row => row.Line == "Insolvencia")),
            rchSummary = ProjectSummary(summaryRows.Where(row => row.Line == "RCH")),
            insolvencyDetail = ProjectDetail(detailRows.Where(row => row.Line == "Insolvencia")),
            rchDetail = ProjectDetail(detailRows.Where(row => row.Line == "RCH"))
        };
    }

    private static async Task<object> GetManagementInsDetailRowsAsync(
        string sql,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var rows = new List<object>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new
            {
                name = reader.GetString(0),
                stage = reader.IsDBNull(1) ? null : reader.GetString(1),
                responsible = reader.IsDBNull(2) ? null : reader.GetString(2),
                total = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                daysOutOfManagement = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
            });
        }

        return new { rows };
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
                JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = d.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = d.bitrix_id
                    AND snapshot.is_deleted = false
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
            JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
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
            JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
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
                SELECT deal.pipeline_id, count(*)::integer AS deals_count
                FROM bitrix.deals deal
                JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = deal.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = deal.bitrix_id
                    AND snapshot.is_deleted = false
                GROUP BY deal.pipeline_id
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
                JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = deal.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = deal.bitrix_id
                    AND snapshot.is_deleted = false
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM bitrix.pipeline_stages stage
                    WHERE stage.pipeline_id = deal.pipeline_id
                      AND stage.bitrix_stage_id IS NOT DISTINCT FROM deal.stage_id
                )
                GROUP BY deal.pipeline_id, deal.stage_id
            ), stage_counts AS (
                SELECT deal.pipeline_id, deal.stage_id, count(*)::integer AS deals_count
                FROM bitrix.deals deal
                JOIN bitrix.entity_snapshots snapshot
                    ON snapshot.connection_id = deal.connection_id
                    AND snapshot.entity_type = 'deal'
                    AND snapshot.bitrix_id = deal.bitrix_id
                    AND snapshot.is_deleted = false
                GROUP BY deal.pipeline_id, deal.stage_id
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
            JOIN bitrix.entity_snapshots snapshot
                ON snapshot.connection_id = d.connection_id
                AND snapshot.entity_type = 'deal'
                AND snapshot.bitrix_id = d.bitrix_id
                AND snapshot.is_deleted = false
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
