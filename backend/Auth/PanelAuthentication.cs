using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace InformesAvanzar.Api.Auth;

public sealed record PanelUser(Guid Id, string Email, string FullName, string RoleCode);

public static class PanelAuthentication
{
    public const string CookieName = "avanzar_panel_session";

    public static async Task EnsureSuperAdminAsync(string email, string password, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = """
            WITH saved_user AS (
                INSERT INTO auth.users (email, full_name, password_hash, status)
                VALUES (lower(@email), 'Superadministrador', crypt(@password, gen_salt('bf', 12)), 'active')
                ON CONFLICT (email) DO UPDATE
                SET password_hash = CASE WHEN auth.users.password_hash IS NULL THEN EXCLUDED.password_hash ELSE auth.users.password_hash END,
                    status = 'active', deleted_at = NULL
                RETURNING id
            )
            INSERT INTO auth.user_roles (user_id, role_id)
            SELECT saved_user.id, roles.id FROM saved_user CROSS JOIN auth.roles roles WHERE roles.code = 'admin'
            ON CONFLICT DO NOTHING;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email.Trim());
        command.Parameters.AddWithValue("password", password);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<PanelUser?> ValidateCredentialsAsync(string email, string password, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.id, u.email, u.full_name, COALESCE(r.code, '')
            FROM auth.users u
            LEFT JOIN auth.user_roles ur ON ur.user_id = u.id
            LEFT JOIN auth.roles r ON r.id = ur.role_id
            WHERE u.email = lower(@email) AND u.status = 'active' AND u.deleted_at IS NULL
              AND u.password_hash IS NOT NULL AND u.password_hash = crypt(@password, u.password_hash)
            ORDER BY CASE WHEN r.code = 'admin' THEN 0 ELSE 1 END
            LIMIT 1;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email.Trim());
        command.Parameters.AddWithValue("password", password);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PanelUser(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3))
            : null;
    }

    public static string CreateToken(PanelUser user, string signingKey)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { user.Id, user.Email, user.FullName, user.RoleCode, exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds() });
        var encodedPayload = Base64Url(payload);
        var signature = Sign(encodedPayload, signingKey);
        return $"{encodedPayload}.{signature}";
    }

    public static PanelUser? ValidateToken(string? token, string signingKey)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.', 2);
        if (parts.Length != 2) return null;
        var expected = Sign(parts[0], signingKey);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(parts[1]), Encoding.UTF8.GetBytes(expected))) return null;
        try
        {
            using var document = JsonDocument.Parse(Convert.FromBase64String(PadBase64(parts[0].Replace('-', '+').Replace('_', '/'))));
            var root = document.RootElement;
            if (root.GetProperty("exp").GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            return new PanelUser(root.GetProperty("Id").GetGuid(), root.GetProperty("Email").GetString()!, root.GetProperty("FullName").GetString()!, root.GetProperty("RoleCode").GetString()!);
        }
        catch { return null; }
    }

    private static string Sign(string payload, string key) => Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(payload)));
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string PadBase64(string value) => value.PadRight(value.Length + ((4 - value.Length % 4) % 4), '=');
}
