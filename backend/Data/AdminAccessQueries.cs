using Npgsql;

namespace InformesAvanzar.Api.Data;

public static class AdminAccessQueries
{
    public static async Task EnsureUserManagementSchemaAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("ALTER TABLE auth.users ADD COLUMN IF NOT EXISTS password_hash text;", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task EnsureReportCatalogAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.report_definitions (code, name, description, query_key, status)
            VALUES
                ('informe_general_comercial', 'Informe General Comercial', 'Informe consolidado del área comercial.', 'informe_general_comercial', 'published'),
                ('fuerza_comercial_diego', 'Fuerza Comercial', 'Panel consolidado de la fuerza comercial.', 'fuerza_comercial_diego', 'published'),
                ('rch_comercial', 'RCH Comercial', 'Negociaciones comerciales RCH.', 'rch_comercial', 'published'),
                ('rch_operativa', 'RCH Operativa', 'Seguimiento operativo RCH.', 'rch_operativa', 'published'),
                ('pnnc_comercial', 'PNNC Comercial', 'Negociaciones comerciales PNNC.', 'pnnc_comercial', 'published'),
                ('pnnc_operativa', 'PNNC Operativa', 'Seguimiento operativo PNNC.', 'pnnc_operativa', 'published'),
                ('informe_gerencia_2026_2027', 'Informe Gerencia 2026 y 2027', 'Panel ejecutivo para seguimiento gerencial de indicadores 2026 y 2027.', 'informe_gerencia_2026_2027', 'published')
            ON CONFLICT (code) DO UPDATE
            SET name = EXCLUDED.name,
                description = EXCLUDED.description,
                query_key = EXCLUDED.query_key,
                status = EXCLUDED.status,
                deleted_at = NULL;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<object> GetAccessManagementAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var users = new List<object>();
        const string usersSql = """
            SELECT u.id, u.full_name, u.email, u.status,
                   COALESCE(array_agg(ur.role_id) FILTER (WHERE ur.role_id IS NOT NULL), ARRAY[]::uuid[]) AS role_ids
            FROM auth.users u
            LEFT JOIN auth.user_roles ur ON ur.user_id = u.id
            WHERE u.deleted_at IS NULL
            GROUP BY u.id
            ORDER BY u.full_name;
            """;
        await using (var command = new NpgsqlCommand(usersSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                users.Add(new { id = reader.GetGuid(0), fullName = reader.GetString(1), email = reader.GetString(2), status = reader.GetString(3), roleIds = reader.GetFieldValue<Guid[]>(4) });
        }

        var roles = new List<object>();
        const string rolesSql = """
            SELECT r.id, r.code, r.name, r.description, r.is_system,
                   COALESCE(array_agg(rp.permission_id) FILTER (WHERE rp.permission_id IS NOT NULL), ARRAY[]::uuid[]) AS permission_ids
            FROM auth.roles r
            LEFT JOIN auth.role_permissions rp ON rp.role_id = r.id
            GROUP BY r.id
            ORDER BY r.name;
            """;
        await using (var command = new NpgsqlCommand(rolesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                roles.Add(new { id = reader.GetGuid(0), code = reader.GetString(1), name = reader.GetString(2), description = reader.IsDBNull(3) ? null : reader.GetString(3), isSystem = reader.GetBoolean(4), permissionIds = reader.GetFieldValue<Guid[]>(5) });
        }

        var permissions = new List<object>();
        await using (var command = new NpgsqlCommand("SELECT id, code, name, description FROM auth.permissions ORDER BY name;", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                permissions.Add(new { id = reader.GetGuid(0), code = reader.GetString(1), name = reader.GetString(2), description = reader.IsDBNull(3) ? null : reader.GetString(3) });
        }

        var reports = new List<object>();
        const string reportsSql = """
            SELECT rd.id, rd.code, rd.name,
                   COALESCE(jsonb_object_agg(ra.role_id::text, ra.access_level) FILTER (WHERE ra.role_id IS NOT NULL), '{}'::jsonb)::text,
                   COALESCE(jsonb_object_agg(ra.user_id::text, ra.access_level) FILTER (WHERE ra.user_id IS NOT NULL), '{}'::jsonb)::text
            FROM reporting.report_definitions rd
            LEFT JOIN reporting.report_access ra ON ra.report_definition_id = rd.id
            WHERE rd.deleted_at IS NULL
              AND rd.code <> ALL(@hiddenReportCodes)
            GROUP BY rd.id
            ORDER BY rd.name;
            """;
        await using (var command = new NpgsqlCommand(reportsSql, connection))
        {
            command.Parameters.AddWithValue("hiddenReportCodes", new[] { "marketing" });
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                reports.Add(new {
                    id = reader.GetGuid(0), code = reader.GetString(1), name = reader.GetString(2),
                    roleAccess = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(3)),
                    userAccess = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(4))
                });
        }
        }

        return new { users, roles, permissions, reports };
    }

    public static async Task<Guid> CreateUserAsync(string fullName, string email, string password, Guid? roleId, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        const string sql = "INSERT INTO auth.users (full_name, email, password_hash, status) VALUES (@fullName, lower(@email), crypt(@password, gen_salt('bf', 12)), 'active') RETURNING id;";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("fullName", fullName.Trim());
        command.Parameters.AddWithValue("email", email.Trim());
        command.Parameters.AddWithValue("password", password);
        var userId = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("No fue posible crear el usuario."));
        if (roleId is not null)
        {
            await using var roleCommand = new NpgsqlCommand("INSERT INTO auth.user_roles (user_id, role_id) VALUES (@userId, @roleId) ON CONFLICT DO NOTHING;", connection, transaction);
            roleCommand.Parameters.AddWithValue("userId", userId);
            roleCommand.Parameters.AddWithValue("roleId", roleId.Value);
            await roleCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return userId;
    }

    public static async Task UpdateUserAsync(Guid userId, string fullName, string email, string status, Guid? roleId, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        const string updateSql = "UPDATE auth.users SET full_name = @fullName, email = lower(@email), status = @status WHERE id = @userId AND deleted_at IS NULL;";
        await using (var command = new NpgsqlCommand(updateSql, connection, transaction))
        {
            command.Parameters.AddWithValue("userId", userId);
            command.Parameters.AddWithValue("fullName", fullName.Trim());
            command.Parameters.AddWithValue("email", email.Trim());
            command.Parameters.AddWithValue("status", status is "disabled" or "invited" ? status : "active");
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                throw new InvalidOperationException("El usuario no existe.");
        }
        await using (var delete = new NpgsqlCommand("DELETE FROM auth.user_roles WHERE user_id = @userId;", connection, transaction))
        {
            delete.Parameters.AddWithValue("userId", userId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        if (roleId is not null)
        {
            await using var insert = new NpgsqlCommand("INSERT INTO auth.user_roles (user_id, role_id) VALUES (@userId, @roleId);", connection, transaction);
            insert.Parameters.AddWithValue("userId", userId);
            insert.Parameters.AddWithValue("roleId", roleId.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task SetUserPasswordAsync(Guid userId, string password, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE auth.users SET password_hash = crypt(@password, gen_salt('bf', 12)) WHERE id = @userId AND deleted_at IS NULL;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("password", password);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("El usuario no existe.");
    }

    public static async Task DeleteUserAsync(Guid userId, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE auth.users SET status = 'disabled', deleted_at = now() WHERE id = @userId AND deleted_at IS NULL;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("El usuario no existe.");
    }

    public static async Task SetUserRoleAsync(Guid userId, Guid? roleId, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var delete = new NpgsqlCommand("DELETE FROM auth.user_roles WHERE user_id = @userId;", connection, transaction))
        {
            delete.Parameters.AddWithValue("userId", userId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        if (roleId is not null)
        {
            await using var insert = new NpgsqlCommand("INSERT INTO auth.user_roles (user_id, role_id) VALUES (@userId, @roleId);", connection, transaction);
            insert.Parameters.AddWithValue("userId", userId);
            insert.Parameters.AddWithValue("roleId", roleId.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task SetRolePermissionAsync(Guid roleId, Guid permissionId, bool enabled, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var sql = enabled
            ? "INSERT INTO auth.role_permissions (role_id, permission_id) VALUES (@roleId, @permissionId) ON CONFLICT DO NOTHING;"
            : "DELETE FROM auth.role_permissions WHERE role_id = @roleId AND permission_id = @permissionId;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("permissionId", permissionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task SetReportRoleAccessAsync(Guid reportId, Guid roleId, bool enabled, string accessLevel, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var sql = enabled
            ? """INSERT INTO reporting.report_access (report_definition_id, role_id, access_level) VALUES (@reportId, @roleId, @accessLevel) ON CONFLICT (report_definition_id, role_id) WHERE role_id IS NOT NULL DO UPDATE SET access_level = EXCLUDED.access_level;"""
            : "DELETE FROM reporting.report_access WHERE report_definition_id = @reportId AND role_id = @roleId;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("reportId", reportId);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("accessLevel", accessLevel is "editor" or "owner" ? accessLevel : "viewer");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task SetReportUserAccessAsync(Guid reportId, Guid userId, bool enabled, string accessLevel, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var sql = enabled
            ? """INSERT INTO reporting.report_access (report_definition_id, user_id, access_level) VALUES (@reportId, @userId, @accessLevel) ON CONFLICT (report_definition_id, user_id) WHERE user_id IS NOT NULL DO UPDATE SET access_level = EXCLUDED.access_level;"""
            : "DELETE FROM reporting.report_access WHERE report_definition_id = @reportId AND user_id = @userId;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("reportId", reportId);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("accessLevel", accessLevel is "editor" or "owner" ? accessLevel : "viewer");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task SetUserReportBlocksAsync(Guid userId, string reportCode, string[] visibleBlocks, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO reporting.user_report_block_settings (user_id, report_code, visible_blocks, updated_at)
            VALUES (@userId, @reportCode, @visibleBlocks, now())
            ON CONFLICT (user_id, report_code) DO UPDATE
            SET visible_blocks = EXCLUDED.visible_blocks,
                updated_at = now();
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("reportCode", reportCode);
        command.Parameters.AddWithValue("visibleBlocks", visibleBlocks);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
