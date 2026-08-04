using Npgsql;

namespace InformesAvanzar.Api.Data;

public static class AdminAccessQueries
{
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
                   COALESCE(jsonb_object_agg(ra.role_id::text, ra.access_level) FILTER (WHERE ra.role_id IS NOT NULL), '{}'::jsonb)::text
            FROM reporting.report_definitions rd
            LEFT JOIN reporting.report_access ra ON ra.report_definition_id = rd.id AND ra.role_id IS NOT NULL
            WHERE rd.deleted_at IS NULL
            GROUP BY rd.id
            ORDER BY rd.name;
            """;
        await using (var command = new NpgsqlCommand(reportsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                reports.Add(new { id = reader.GetGuid(0), code = reader.GetString(1), name = reader.GetString(2), roleAccess = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(3)) });
        }

        return new { users, roles, permissions, reports };
    }

    public static async Task<Guid> CreateUserAsync(string fullName, string email, Guid? roleId, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        const string sql = "INSERT INTO auth.users (full_name, email, status) VALUES (@fullName, lower(@email), 'active') RETURNING id;";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("fullName", fullName.Trim());
        command.Parameters.AddWithValue("email", email.Trim());
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
}
