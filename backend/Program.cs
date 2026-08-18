using InformesAvanzar.Api.Bitrix;
using InformesAvanzar.Api.Auth;
using System.Text.Json;
using InformesAvanzar.Api.Configuration;
using InformesAvanzar.Api.Data;
using InformesAvanzar.Api.Reports;
using InformesAvanzar.Api.Sync;
using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;
using System.Diagnostics;

EnvFileLoader.LoadNearest(".env");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.Configure<BitrixOptions>(builder.Configuration.GetSection("Bitrix"));
builder.Services.PostConfigure<BitrixOptions>(options =>
{
    options.WebhookUrl = builder.Configuration["BITRIX_WEBHOOK_URL"] ?? options.WebhookUrl;
    options.OutgoingWebhookToken = builder.Configuration["BITRIX_OUTGOING_WEBHOOK_TOKEN"] ?? options.OutgoingWebhookToken;
    options.WebhookAllowedPipelineDomains = builder.Configuration["BITRIX_WEBHOOK_ALLOWED_PIPELINE_DOMAINS"] ?? options.WebhookAllowedPipelineDomains;
});
builder.Services.AddHttpClient<IBitrixClient, BitrixClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
builder.Services.AddScoped<IDatabaseHealthCheck, DatabaseHealthCheck>();
builder.Services.AddScoped<IReportAccessService, ReportAccessService>();
builder.Services.AddSingleton<IBitrixPipelineCatalog, BitrixPipelineCatalog>();
builder.Services.AddScoped<IBitrixSyncRepository, BitrixSyncRepository>();
builder.Services.AddScoped<IBitrixSynchronizer, BitrixSynchronizer>();
builder.Services.AddScoped<IBitrixUserSyncService, BitrixUserSyncService>();
builder.Services.AddScoped<IBitrixStageSyncService, BitrixStageSyncService>();
builder.Services.AddScoped<IBitrixDealSyncService, BitrixDealSyncService>();
builder.Services.AddScoped<IBitrixActivitySyncService, BitrixActivitySyncService>();
builder.Services.AddScoped<IBitrixMassiveSyncService, BitrixMassiveSyncService>();
builder.Services.AddScoped<IBitrixWebhookPendingSyncService, BitrixWebhookPendingSyncService>();

var app = builder.Build();

var panelSigningKey = app.Configuration["PANEL_AUTH_SIGNING_KEY"] ?? app.Configuration["ADMIN_API_KEY"] ?? throw new InvalidOperationException("Configure PANEL_AUTH_SIGNING_KEY o ADMIN_API_KEY.");
var superAdminEmail = app.Configuration["SUPERADMIN_EMAIL"] ?? "superadmin@avanzarsoluciones.com";
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (SyncAlreadyRunningException exception) when (!context.Response.HasStarted)
    {
        app.Logger.LogInformation(exception, "A Bitrix synchronization request was rejected because another run is active.");
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "sync_already_running",
            message = exception.Message
        }, context.RequestAborted);
    }
});
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "/";
    var isPublic = path is "/login.html" or "/login.js" or "/health" or "/health/db" or "/api/bitrix/webhooks/deals" || path.StartsWith("/assets/") || path == "/styles.css" || path == "/api/auth/login";
    var panelUser = PanelAuthentication.ValidateToken(context.Request.Cookies[PanelAuthentication.CookieName], panelSigningKey);
    if (panelUser is not null)
    {
        var dataSource = context.RequestServices.GetRequiredService<NpgsqlDataSource>();
        panelUser = await PanelAuthentication.GetCurrentUserAsync(panelUser.Id, dataSource, context.RequestAborted);
    }
    if (panelUser is not null) context.Items["PanelUser"] = panelUser;
    if (!isPublic && panelUser is null)
    {
        if (path.StartsWith("/api/")) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        context.Response.Redirect($"/login.html?returnUrl={Uri.EscapeDataString(path)}");
        return;
    }
    if (path == "/login.html" && panelUser is not null) { context.Response.Redirect("/"); return; }

    var isSuperAdmin = panelUser is not null && string.Equals(panelUser.Email, superAdminEmail, StringComparison.OrdinalIgnoreCase);
    var isSuperAdminPage = path is "/usuarios.html" or "/estructura-comercial.html";
    var isSuperAdminApi = path.StartsWith("/api/admin") || path.StartsWith("/api/organization/commercial");
    if ((isSuperAdminPage || isSuperAdminApi) && !isSuperAdmin)
    {
        if (path.StartsWith("/api/")) context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else context.Response.Redirect("/informes.html?access=denied");
        return;
    }

    if (path == "/reporte.html" && panelUser is not null && panelUser.RoleCode != "admin")
    {
        var reportCode = context.Request.Query["id"].ToString();
        var reportAccess = context.RequestServices.GetRequiredService<IReportAccessService>();
        if (string.IsNullOrWhiteSpace(reportCode) || !await reportAccess.UserCanAccessReportAsync(panelUser.Id, reportCode, context.RequestAborted))
        {
            context.Response.Redirect("/informes.html?access=denied");
            return;
        }
    }

    if (panelUser is not null && panelUser.RoleCode != "admin" && path.StartsWith("/api/"))
    {
        string[] requiredReportCodes = path switch
        {
            _ when path.StartsWith("/api/reports/fuerza-comercial-diego/") =>
                ["fuerza_comercial_diego", "informe_general_comercial"],
            _ when path.StartsWith("/api/data/")
                && !string.IsNullOrWhiteSpace(context.Request.Query["pipeline"].ToString()) =>
                [context.Request.Query["pipeline"].ToString()],
            _ => []
        };

        if (requiredReportCodes.Length > 0)
        {
            var reportAccess = context.RequestServices.GetRequiredService<IReportAccessService>();
            var hasReportAccess = false;
            foreach (var reportCode in requiredReportCodes)
            {
                if (await reportAccess.UserCanAccessReportAsync(panelUser.Id, reportCode, context.RequestAborted))
                {
                    hasReportAccess = true;
                    break;
                }
            }

            if (!hasReportAccess)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
    }

    if (panelUser is not null && panelUser.RoleCode != "admin")
    {
        string[] requiredPermissions = path switch
        {
            "/usuarios.html" => ["users.manage", "roles.manage", "reports.manage"],
            "/estructura-comercial.html" => ["users.manage", "roles.manage"],
            "/sincronizacion.html" => ["bitrix.sync.view"],
            _ when path.StartsWith("/api/organization/commercial/") && context.Request.Method != "GET" => ["users.manage", "roles.manage", "reports.manage"],
            _ when path.StartsWith("/api/bitrix/sync/") && context.Request.Method != "GET" => ["bitrix.sync.run"],
            _ => []
        };
        if (requiredPermissions.Length > 0)
        {
            var dataSource = context.RequestServices.GetRequiredService<NpgsqlDataSource>();
            if (!await PanelAuthentication.HasAnyPermissionAsync(panelUser.Id, dataSource, context.RequestAborted, requiredPermissions))
            {
                if (path.StartsWith("/api/")) context.Response.StatusCode = StatusCodes.Status403Forbidden;
                else context.Response.Redirect("/informes.html?access=denied");
                return;
            }
        }
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (!context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            && !context.File.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});

app.MapPost("/api/auth/login", async (JsonElement body, HttpContext context, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var email = body.TryGetProperty("email", out var emailProperty) ? emailProperty.GetString() : null;
    var password = body.TryGetProperty("password", out var passwordProperty) ? passwordProperty.GetString() : null;
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return Results.Unauthorized();
    var user = await PanelAuthentication.ValidateCredentialsAsync(email, password, dataSource, cancellationToken);
    if (user is null) return Results.Unauthorized();
    context.Response.Cookies.Append(PanelAuthentication.CookieName, PanelAuthentication.CreateToken(user, panelSigningKey), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps, MaxAge = TimeSpan.FromHours(12), Path = "/" });
    return Results.Ok(new { user.FullName, user.Email, user.RoleCode });
});

app.MapPost("/api/auth/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete(PanelAuthentication.CookieName, new CookieOptions { Path = "/" });
    return Results.NoContent();
});

app.MapGet("/api/auth/me", async (HttpContext context, IReportAccessService reportAccess, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var user = (PanelUser)context.Items["PanelUser"]!;
    var reportCodes = user.RoleCode == "admin"
        ? new[] { "informe_general_comercial", "fuerza_comercial_diego", "rch_comercial", "rch_operativa", "pnnc_comercial", "pnnc_operativa", "informe_gerencia_2026_2027" }
        : await reportAccess.GetAccessibleReportCodesAsync(user.Id, cancellationToken);
    var permissions = await PanelAuthentication.GetPermissionCodesAsync(user.Id, dataSource, cancellationToken);
    var blockAccess = user.RoleCode == "admin"
        ? (Configured: false, Blocks: Array.Empty<string>())
        : await OrganizationQueries.GetUserBlockAccessAsync(user.Id, "informe_general_comercial", dataSource, cancellationToken);
    var teamScope = user.RoleCode == "admin"
        ? null
        : await OrganizationQueries.GetUserTeamScopeAsync(user.Id, dataSource, cancellationToken);
    var isSuperAdmin = string.Equals(user.Email, superAdminEmail, StringComparison.OrdinalIgnoreCase);
    return Results.Ok(new { user.Id, user.Email, user.FullName, user.RoleCode, isSuperAdmin, accessibleReportCodes = reportCodes, permissions, teamScope, generalCommercialBlocksConfigured = user.RoleCode != "admin" || blockAccess.Configured, generalCommercialBlockCodes = blockAccess.Blocks });
});
app.MapGet("/api/organization/commercial", async (NpgsqlDataSource dataSource, CancellationToken cancellationToken) => Results.Ok(await OrganizationQueries.GetCommercialStructureAsync(dataSource, cancellationToken)));
app.MapPut("/api/organization/commercial/{departmentId}/settings", async (string departmentId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) => { var role=body.TryGetProperty("roleLabel",out var roleProperty)?roleProperty.GetString()??"viewer":"viewer";var email=body.TryGetProperty("email",out var emailProperty)?emailProperty.GetString():null;var reports=body.TryGetProperty("visibleReports",out var reportsProperty)&&reportsProperty.ValueKind==JsonValueKind.Array?reportsProperty.EnumerateArray().Select(item=>item.GetString()).Where(item=>!string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray():Array.Empty<string>();var blocks=body.TryGetProperty("visibleBlocks",out var blocksProperty)&&blocksProperty.ValueKind==JsonValueKind.Array?blocksProperty.EnumerateArray().Select(item=>item.GetString()).Where(item=>!string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray():Array.Empty<string>();await OrganizationQueries.SetSettingsAsync(departmentId,email,role,reports,blocks,dataSource,cancellationToken);return Results.NoContent(); });

app.MapGet("/health", (IHostEnvironment environment) => Results.Ok(new
{
    status = "ok",
    environment = environment.EnvironmentName
}));

app.MapGet("/api/dashboard/overview", () => Results.Ok(new
{
    totals = new
    {
        pipelines = 4,
        entityTypes = 6,
        syncedDeals = 0,
        syncedUsers = 0
    },
    cards = new[]
    {
        new { label = "Pipelines", value = "4", note = "Sync inicial acotada", detail = "RCH y PNNC", tone = "default" },
        new { label = "Negociaciones", value = "0", note = "Pendiente de sincronizar", detail = "crm.deal.list", tone = "default" },
        new { label = "Usuarios", value = "0", note = "Responsables Bitrix", detail = "user.get", tone = "default" },
        new { label = "Campos personalizados", value = "UF_CRM", note = "Se guardan completos", detail = "JSONB + valores", tone = "success" },
        new { label = "Estado", value = "Listo", note = "Esperando primera sync", detail = "PostgreSQL local", tone = "success" }
    },
    pipelines = new[]
    {
        new { slug = "rch_comercial", name = "RCH Comercial", categoryId = 8, area = "Comercial", status = "Activa", entities = "Deals, etapas, usuarios, actividades" },
        new { slug = "rch_operativa", name = "RCH Operativa", categoryId = 10, area = "Operaciones", status = "Activa", entities = "Deals, etapas, usuarios, tareas" },
        new { slug = "pnnc_comercial", name = "PNNC Comercial", categoryId = 26, area = "Comercial", status = "Activa", entities = "Deals, etapas, usuarios, actividades" },
        new { slug = "pnnc_operativa", name = "PNNC Operativa", categoryId = 28, area = "Operaciones", status = "Activa", entities = "Deals, etapas, usuarios, tareas" }
    }
}));

app.MapGet("/api/bitrix/pipelines", (IBitrixPipelineCatalog catalog) => Results.Ok(catalog.ListDefaults()));

app.MapGet("/api/data/deals", async (
    string? pipeline,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetDealsAsync(pipeline ?? "all", dataSource, cancellationToken);
});

app.MapGet("/api/data/stage-distribution", async (
    string? pipeline,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetStageDistributionAsync(pipeline ?? "all", dataSource, cancellationToken);
});

app.MapGet("/api/data/pipeline-inventory", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetPipelineInventoryAsync(dataSource, cancellationToken);
});

app.MapGet("/api/data/responsible-distribution", async (
    string? pipeline,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetResponsibleDistributionAsync(pipeline ?? "all", dataSource, cancellationToken);
});

app.MapGet("/api/data/sync-summary", async (
    string? pipeline,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetSyncSummaryAsync(pipeline ?? "all", dataSource, cancellationToken);
});

app.MapGet("/api/data/sync-state", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetSyncStateAsync(dataSource, cancellationToken);
});

app.MapGet("/api/data/users", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetUsersAsync(dataSource, cancellationToken);
});

app.MapGet("/api/data/sync-history", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetSyncHistoryAsync(dataSource, cancellationToken);
});

app.MapGet("/api/data/stages", async (
    string? pipeline,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return await BitrixDataQueries.GetStagesAsync(pipeline ?? "all", dataSource, cancellationToken);
});

app.MapGet("/api/data/pipelines", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetPipelinesAsync(dataSource, cancellationToken));
});

var adminApi = app.MapGroup("/api/admin");
adminApi.AddEndpointFilter(async (context, next) =>
{
    if (context.HttpContext.Items["PanelUser"] is PanelUser panelUser)
    {
        if (string.Equals(panelUser.Email, superAdminEmail, StringComparison.OrdinalIgnoreCase)) return await next(context);
        return Results.Forbid();
    }
    var configuredKey = app.Configuration["ADMIN_API_KEY"];
    if (string.IsNullOrWhiteSpace(configuredKey))
        return Results.Problem("Configure ADMIN_API_KEY para habilitar la administración de accesos.", statusCode: StatusCodes.Status503ServiceUnavailable);
    var suppliedKey = context.HttpContext.Request.Headers["X-Admin-Key"].ToString();
    return string.Equals(configuredKey, suppliedKey, StringComparison.Ordinal)
        ? await next(context)
        : Results.Unauthorized();
});

adminApi.MapGet("/access-management", async (NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
    Results.Ok(await AdminAccessQueries.GetAccessManagementAsync(dataSource, cancellationToken)));

adminApi.MapPost("/organization/{departmentId}/create-user", async (string departmentId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var fullName = body.TryGetProperty("fullName", out var nameProperty) ? nameProperty.GetString() : null;
    var email = body.TryGetProperty("email", out var emailProperty) ? emailProperty.GetString() : null;
    var roleLabel = body.TryGetProperty("roleLabel", out var roleProperty) ? roleProperty.GetString() ?? "viewer" : "viewer";
    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email)) return Results.BadRequest(new { message = "El responsable debe tener nombre y correo en Bitrix." });
    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
    await using (var existsCommand = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM auth.users WHERE lower(email)=lower(@email) AND deleted_at IS NULL);", connection))
    {
        existsCommand.Parameters.AddWithValue("email", email.Trim());
        if ((bool)(await existsCommand.ExecuteScalarAsync(cancellationToken) ?? false)) return Results.Conflict(new { message = "Este correo ya está registrado en Usuarios y roles." });
    }
    var roleCode = roleLabel is "coordinator" or "leader" ? "report_manager" : "report_viewer";
    Guid? roleId;
    await using (var roleCommand = new NpgsqlCommand("SELECT id FROM auth.roles WHERE code=@code;", connection))
    {
        roleCommand.Parameters.AddWithValue("code", roleCode);
        roleId = await roleCommand.ExecuteScalarAsync(cancellationToken) as Guid?;
    }
    var rolePrefix = roleLabel switch { "coordinator" => "Coord", "leader" => "Lider", "advisor" => "Asesor", _ => "Consulta" };
    var password = $"Avz-{rolePrefix}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(7))}!";
    var userId = await AdminAccessQueries.CreateUserAsync(fullName, email, password, roleId, dataSource, cancellationToken);
    string[] savedReports = Array.Empty<string>();
    string[] savedBlocks = Array.Empty<string>();
    await using (var settingsCommand = new NpgsqlCommand("SELECT visible_reports,visible_blocks FROM reporting.organization_access WHERE department_id=@departmentId;", connection))
    {
        settingsCommand.Parameters.AddWithValue("departmentId", departmentId);
        await using var reader = await settingsCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            savedReports = reader.GetFieldValue<string[]>(0);
            savedBlocks = reader.GetFieldValue<string[]>(1);
        }
    }
    if (savedReports.Length > 0 || savedBlocks.Length > 0)
        await OrganizationQueries.SetSettingsAsync(departmentId, email, roleLabel, savedReports, savedBlocks, dataSource, cancellationToken);
    return Results.Created($"/api/admin/users/{userId}", new { id = userId, temporaryPassword = password, roleCode, departmentId });
});

adminApi.MapPost("/users", async (JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var fullName = body.TryGetProperty("fullName", out var nameProperty) ? nameProperty.GetString() : null;
    var email = body.TryGetProperty("email", out var emailProperty) ? emailProperty.GetString() : null;
    var password = body.TryGetProperty("password", out var passwordProperty) ? passwordProperty.GetString() : null;
    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return Results.BadRequest(new { message = "Nombre, correo y contraseña son obligatorios." });
    if (password.Length < 8)
        return Results.BadRequest(new { message = "La contraseña debe tener mínimo 8 caracteres." });
    Guid? roleId = body.TryGetProperty("roleId", out var roleProperty) && Guid.TryParse(roleProperty.GetString(), out var parsedRole) ? parsedRole : null;
    var userId = await AdminAccessQueries.CreateUserAsync(fullName, email, password, roleId, dataSource, cancellationToken);
    return Results.Created($"/api/admin/users/{userId}", new { id = userId });
});

adminApi.MapPut("/users/{userId:guid}", async (Guid userId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var fullName = body.TryGetProperty("fullName", out var nameProperty) ? nameProperty.GetString() : null;
    var email = body.TryGetProperty("email", out var emailProperty) ? emailProperty.GetString() : null;
    var status = body.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() ?? "active" : "active";
    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        return Results.BadRequest(new { message = "Nombre y correo son obligatorios." });
    Guid? roleId = body.TryGetProperty("roleId", out var roleProperty) && Guid.TryParse(roleProperty.GetString(), out var parsedRole) ? parsedRole : null;
    await AdminAccessQueries.UpdateUserAsync(userId, fullName, email, status, roleId, dataSource, cancellationToken);
    return Results.NoContent();
});

adminApi.MapPut("/users/{userId:guid}/password", async (Guid userId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var password = body.TryGetProperty("password", out var passwordProperty) ? passwordProperty.GetString() : null;
    if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        return Results.BadRequest(new { message = "La contraseña debe tener mínimo 8 caracteres." });
    await AdminAccessQueries.SetUserPasswordAsync(userId, password, dataSource, cancellationToken);
    return Results.NoContent();
});

adminApi.MapDelete("/users/{userId:guid}", async (Guid userId, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    await AdminAccessQueries.DeleteUserAsync(userId, dataSource, cancellationToken);
    return Results.NoContent();
});

adminApi.MapPut("/users/{userId:guid}/role", async (Guid userId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    Guid? roleId = body.TryGetProperty("roleId", out var roleProperty) && Guid.TryParse(roleProperty.GetString(), out var parsedRole) ? parsedRole : null;
    await AdminAccessQueries.SetUserRoleAsync(userId, roleId, dataSource, cancellationToken);
    return Results.NoContent();
});

adminApi.MapPut("/roles/{roleId:guid}/permissions/{permissionId:guid}", async (Guid roleId, Guid permissionId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var enabled = body.TryGetProperty("enabled", out var enabledProperty) && enabledProperty.GetBoolean();
    await AdminAccessQueries.SetRolePermissionAsync(roleId, permissionId, enabled, dataSource, cancellationToken);
    return Results.NoContent();
});

adminApi.MapPut("/reports/{reportId:guid}/roles/{roleId:guid}", async (Guid reportId, Guid roleId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var enabled = body.TryGetProperty("enabled", out var enabledProperty) && enabledProperty.GetBoolean();
    var accessLevel = body.TryGetProperty("accessLevel", out var levelProperty) ? levelProperty.GetString() ?? "viewer" : "viewer";
    await AdminAccessQueries.SetReportRoleAccessAsync(reportId, roleId, enabled, accessLevel, dataSource, cancellationToken);
    return Results.NoContent();
});

adminApi.MapPut("/reports/{reportId:guid}/users/{userId:guid}", async (Guid reportId, Guid userId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var enabled = body.TryGetProperty("enabled", out var enabledProperty) && enabledProperty.GetBoolean();
    var accessLevel = body.TryGetProperty("accessLevel", out var levelProperty) ? levelProperty.GetString() ?? "viewer" : "viewer";
    await AdminAccessQueries.SetReportUserAccessAsync(reportId, userId, enabled, accessLevel, dataSource, cancellationToken);
    return Results.NoContent();
});

adminApi.MapPut("/reports/informe-general/users/{userId:guid}/blocks", async (Guid userId, JsonElement body, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    var visibleBlocks = body.TryGetProperty("visibleBlocks", out var blocksProperty) && blocksProperty.ValueKind == JsonValueKind.Array
        ? blocksProperty.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().Distinct().ToArray()
        : Array.Empty<string>();
    await AdminAccessQueries.SetUserReportBlocksAsync(userId, "informe_general_comercial", visibleBlocks, dataSource, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/api/reports/fuerza-comercial-diego/valores-radicados", async (
    int? year,
    DateTime? from,
    DateTime? to,
    string? month,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoRadicatedValuesAsync(
        selectedYear,
        from,
        to,
        month,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/fuerza-comercial-diego/dashboard", async (
    int? year,
    DateTime? from,
    DateTime? to,
    string? month,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoCommercialDashboardAsync(
        selectedYear,
        from,
        to,
        month,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/fuerza-comercial-diego/cartera-recaudada", async (
    int? year,
    DateTime? from,
    DateTime? to,
    string? month,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoPortfolioCollectionsAsync(
        selectedYear,
        from,
        to,
        month,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/fuerza-comercial-diego/liderazgo-comisiones", async (
    int? year,
    DateTime? from,
    DateTime? to,
    string? month,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoLeadershipAndCommissionsAsync(
        selectedYear,
        from,
        to,
        month,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/fuerza-comercial-diego/jerarquia-filtros", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
    Results.Ok(await BitrixDataQueries.GetCommercialFilterHierarchyAsync(dataSource, cancellationToken)));

app.MapGet("/api/reports/gerencia/comercial-cumplimiento", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetManagementCommercialComplianceAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/comercial-cumplimiento-mensual", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetManagementCommercialMonthlyComplianceAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/posible-cierre", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementPossibleCloseAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/detalle-pnnc", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetManagementPnncDetailComplianceAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/promedio-rch", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetManagementRchAccumulatedAverageAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/rch-operativa-procesos", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetManagementRchOperationalProcessesAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/rch-aprobados-banco", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetManagementRchApprovedByBankAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/pnnc-operativa-procesos-2025", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : 2025;
    return Results.Ok(await BitrixDataQueries.GetManagementPnncOperationalProcesses2025Async(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/pnnc-operativa-gestion", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementPnncOperationalManagementAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/pnnc-operativa-insolvencia-2", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementPnncOperationalInsolvencyTwoAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/pnnc-operativa-detalle", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementPnncOperationalDetailAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/pnnc-lp-compliance-2025", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementPnncLpCompliance2025Async(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/lp-monthly-tasks", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : 2025;
    return Results.Ok(await BitrixDataQueries.GetManagementLpMonthlyTasksAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/lp-weekly-tasks", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : 2025;
    return Results.Ok(await BitrixDataQueries.GetManagementLpWeeklyTasksAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/lp-embargos-tasks", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : 2025;
    return Results.Ok(await BitrixDataQueries.GetManagementLpSpecialMonthlyTasksAsync(
        selectedYear,
        "941",
        "INS Embargos",
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/lp-libranza-tasks", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : 2025;
    return Results.Ok(await BitrixDataQueries.GetManagementLpSpecialMonthlyTasksAsync(
        selectedYear,
        "941",
        "INS Libranza",
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/ins-embargos-detail", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementInsEmbargosDetailAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/ins-libranza-detail", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementInsLibranzaDetailAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/seguros-cumplimiento", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementInsuranceComplianceAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/seguros-operaciones-mensuales", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementInsuranceMonthlyOperationsAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/seguros-fuera-tiempo", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementInsuranceOutOfTimeAsync(
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/servicio-cliente-resumen", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementCustomerServiceSummaryAsync(
        year ?? 2026,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/servicio-cliente-graficas", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementCustomerServiceChartsAsync(
        year ?? 2026,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/servicio-cliente-promedio-respuesta", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementCustomerServiceResponseAverageAsync(
        year ?? 2026,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/gerencia/servicio-cliente-desistimientos", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(await BitrixDataQueries.GetManagementCustomerServiceWithdrawalsAsync(
        year ?? 2026,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/catalog", () => Results.Ok(new[]
{
    new
    {
        key = "usuarios",
        title = "Usuarios y responsables",
        bitrixMethod = "user.get",
        targetTable = "bitrix.users",
        requiresScope = "user",
        columns = new[] { "ID Bitrix", "Nombre", "Email", "Departamento", "Activo", "Ultimo cambio" },
        use = "Asignacion de responsables, filtros por asesor, permisos y auditoria."
    },
    new
    {
        key = "pipelines",
        title = "Pipelines y etapas",
        bitrixMethod = "crm.dealcategory.stage.list",
        targetTable = "bitrix.pipelines / bitrix.pipeline_stages",
        requiresScope = "crm",
        columns = new[] { "Pipeline", "Categoria", "Area", "Etapa", "Orden", "Tipo" },
        use = "Estructura operativa de RCH y PNNC por area comercial u operativa."
    },
    new
    {
        key = "negociaciones",
        title = "Negociaciones",
        bitrixMethod = "crm.deal.list",
        targetTable = "bitrix.deals / bitrix.entity_snapshots",
        requiresScope = "crm",
        columns = new[] { "Deal", "Titulo", "Pipeline", "Etapa", "Responsable", "Cliente", "Valor", "Fecha modificacion" },
        use = "Base principal para informes por pipeline, etapa, responsable, cliente y estado."
    },
    new
    {
        key = "campos",
        title = "Campos personalizados",
        bitrixMethod = "crm.deal.fields",
        targetTable = "bitrix.custom_fields / bitrix.entity_custom_values",
        requiresScope = "crm",
        columns = new[] { "Codigo UF_CRM", "Nombre", "Tipo", "Multiple", "Entidad", "Valor normalizado" },
        use = "Filtros e indicadores propios de cada area sin perder campos originales de Bitrix."
    },
    new
    {
        key = "actividades",
        title = "Actividades CRM",
        bitrixMethod = "crm.activity.list",
        targetTable = "bitrix.activities",
        requiresScope = "crm",
        columns = new[] { "Actividad", "Deal", "Tipo", "Asunto", "Responsable", "Completada", "Fecha limite" },
        use = "Seguimiento de llamadas, reuniones, correos y gestiones asociadas a negociaciones."
    },
    new
    {
        key = "tareas",
        title = "Tareas",
        bitrixMethod = "tasks.task.list",
        targetTable = "bitrix.tasks",
        requiresScope = "tasks",
        columns = new[] { "Tarea", "Titulo", "Relacionado", "Responsable", "Estado", "Prioridad", "Fecha limite" },
        use = "Gestion operativa pendiente, responsables, vencimientos y carga de trabajo."
    },
    new
    {
        key = "comentarios",
        title = "Comentarios y trazabilidad",
        bitrixMethod = "crm.timeline.comment.list",
        targetTable = "bitrix.timeline_comments",
        requiresScope = "crm",
        columns = new[] { "Comentario", "Deal", "Autor", "Texto", "Fecha", "Origen" },
        use = "Auditoria de gestiones y contexto historico de cada negociacion."
    }
}));

app.MapGet("/api/bitrix/config", (Microsoft.Extensions.Options.IOptions<BitrixOptions> options) =>
{
    var webhookUrl = options.Value.WebhookUrl;
    return Results.Ok(new
    {
        configured = !string.IsNullOrWhiteSpace(webhookUrl),
        outgoingWebhookConfigured = !string.IsNullOrWhiteSpace(options.Value.OutgoingWebhookToken),
        webhookHost = TryGetHost(webhookUrl),
        scopes = options.Value.Scopes
    });
});

app.MapPost("/api/bitrix/webhooks/deals", async (
    HttpRequest request,
    Microsoft.Extensions.Options.IOptions<BitrixOptions> options,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var configuredToken = options.Value.OutgoingWebhookToken;
    if (!string.IsNullOrWhiteSpace(configuredToken))
    {
        var providedToken = request.Query["token"].FirstOrDefault()
            ?? request.Headers["X-Bitrix-Webhook-Token"].FirstOrDefault();
        if (!string.Equals(providedToken, configuredToken, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }
    }

    var form = request.HasFormContentType
        ? await request.ReadFormAsync(cancellationToken)
        : null;
    var dealId = form?["data[FIELDS][ID]"].FirstOrDefault()
        ?? form?["data[ID]"].FirstOrDefault()
        ?? form?["FIELDS[ID]"].FirstOrDefault()
        ?? form?["ID"].FirstOrDefault()
        ?? request.Query["data[FIELDS][ID]"].FirstOrDefault()
        ?? request.Query["data[ID]"].FirstOrDefault()
        ?? request.Query["FIELDS[ID]"].FirstOrDefault()
        ?? request.Query["ID"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(dealId))
    {
        return Results.BadRequest(new { message = "No se encontro el ID de la negociacion en el webhook." });
    }

    var eventName = form?["event"].FirstOrDefault()
        ?? request.Query["event"].FirstOrDefault()
        ?? "ONCRMDEALUPDATE";
    var portalDomain = form?["auth[domain]"].FirstOrDefault()
        ?? request.Query["auth[domain]"].FirstOrDefault();

    const string sql = """
        INSERT INTO bitrix.outgoing_webhook_events (
            entity_type,
            bitrix_id,
            event_name,
            portal_domain,
            payload,
            status
        )
        VALUES (
            'deal',
            @bitrixId,
            @eventName,
            @portalDomain,
            @payload,
            'pending'
        )
        ON CONFLICT (entity_type, bitrix_id) DO UPDATE
        SET event_name = EXCLUDED.event_name,
            portal_domain = COALESCE(EXCLUDED.portal_domain, bitrix.outgoing_webhook_events.portal_domain),
            payload = EXCLUDED.payload,
            status = 'pending',
            attempts = 0,
            event_count = bitrix.outgoing_webhook_events.event_count + 1,
            last_seen_at = now(),
            processed_at = NULL,
            last_error = NULL;
        """;

    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("bitrixId", dealId);
    command.Parameters.AddWithValue("eventName", eventName);
    command.Parameters.AddWithValue("portalDomain", (object?)portalDomain ?? DBNull.Value);
    command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = "{}";
    await command.ExecuteNonQueryAsync(cancellationToken);

    return Results.Ok(new { status = "queued", bitrixId = dealId });
});

app.MapGet("/api/bitrix/test/users", async (
    IBitrixClient bitrixClient,
    CancellationToken cancellationToken) =>
{
    try
    {
        using var response = await bitrixClient.CallAsync(
            BitrixMethod.UserGet,
            new[]
            {
                new KeyValuePair<string, string>("FILTER[ACTIVE]", "true"),
                new KeyValuePair<string, string>("start", "0")
            },
            cancellationToken);

        var root = response.RootElement.Clone();
        var count = root.TryGetProperty("result", out var result) && result.ValueKind == System.Text.Json.JsonValueKind.Array
            ? result.GetArrayLength()
            : 0;
        var hasError = root.TryGetProperty("error", out var error);

        return Results.Ok(new
        {
            method = BitrixMethod.UserGet,
            ok = !hasError,
            returned = count,
            scope = "user",
            error = hasError ? error.GetString() : null
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            method = BitrixMethod.UserGet,
            ok = false,
            returned = 0,
            scope = "user",
            error = ex.Message
        });
    }
});

app.MapPost("/api/bitrix/sync/users", async (
    IBitrixUserSyncService userSyncService,
    CancellationToken cancellationToken) =>
{
    var result = await userSyncService.SyncUsersAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/bitrix/sync/departments", async (
    IBitrixClient bitrixClient,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var departments = new List<(long Id, string Name, long? ParentId, int Sort, string? HeadId)>();
    for (var start = 0; ; start += 50)
    {
        using var response = await bitrixClient.CallAsync(
            "department.get",
            new[] { new KeyValuePair<string, string>("start", start.ToString()) },
            cancellationToken);

        if (response.RootElement.TryGetProperty("error", out var error))
        {
            var description = response.RootElement.TryGetProperty("error_description", out var detail)
                ? detail.GetString()
                : error.GetString();
            return Results.BadRequest(new { status = "failed", error = description });
        }

        if (!response.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array) break;
        var page = result.EnumerateArray().ToArray();
        departments.AddRange(page.Select(item =>
        {
            var id = long.Parse(item.GetProperty("ID").ToString());
            var name = item.TryGetProperty("NAME", out var departmentName) ? departmentName.ToString() : "Departamento";
            var parentId = item.TryGetProperty("PARENT", out var parent)
                && long.TryParse(parent.ToString(), out var parsedParentId)
                && parsedParentId > 0 ? parsedParentId : (long?)null;
            var sortOrder = item.TryGetProperty("SORT", out var sort)
                && int.TryParse(sort.ToString(), out var parsedSortOrder) ? parsedSortOrder : 0;
            var headId = item.TryGetProperty("UF_HEAD", out var head) && head.ValueKind != JsonValueKind.Null && head.ToString() != "0"
                ? head.ToString()
                : null;
            return (id, name, parentId, sortOrder, headId);
        }));
        if (page.Length < 50) break;
    }

    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

    foreach (var department in departments)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO bitrix.departments (id, name, parent_id, sort_order, head_bitrix_id, updated_at)
            VALUES (@id, @name, NULL, @sort, @headId, now())
            ON CONFLICT (id) DO UPDATE
            SET name = EXCLUDED.name,
                sort_order = EXCLUDED.sort_order,
                head_bitrix_id = EXCLUDED.head_bitrix_id,
                updated_at = now();
            """, connection, transaction);
        command.Parameters.AddWithValue("id", department.Id);
        command.Parameters.AddWithValue("name", department.Name);
        command.Parameters.AddWithValue("sort", department.Sort);
        command.Parameters.AddWithValue("headId", (object?)department.HeadId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    foreach (var department in departments)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE bitrix.departments SET parent_id = @parentId WHERE id = @id;
            """, connection, transaction);
        command.Parameters.AddWithValue("id", department.Id);
        command.Parameters.AddWithValue("parentId", (object?)department.ParentId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    await transaction.CommitAsync(cancellationToken);
    return Results.Ok(new { status = "succeeded", recordsWritten = departments.Count });
});

app.MapPost("/api/bitrix/sync/stages", async (
    IBitrixStageSyncService stageSyncService,
    CancellationToken cancellationToken) =>
{
    var result = await stageSyncService.SyncStagesAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/bitrix/sync/deals/{pipelineSlug}", async (
    string pipelineSlug,
    string? stageId,
    bool? fullHistory,
    bool? coreFieldsOnly,
    int? resumeFrom,
    IBitrixDealSyncService dealSyncService,
    CancellationToken cancellationToken) =>
{
    var result = await dealSyncService.SyncPipelineDealsAsync(
        pipelineSlug,
        stageId,
        cancellationToken,
        createdFrom: fullHistory == true ? DateTimeOffset.UnixEpoch : null,
        reconcileMissing: resumeFrom.GetValueOrDefault() == 0,
        coreFieldsOnly: coreFieldsOnly == true,
        startAt: resumeFrom.GetValueOrDefault());
    return Results.Ok(result);
});

app.MapPost("/api/bitrix/sync/deals/{pipelineSlug}/incremental", async (
    string pipelineSlug,
    IBitrixDealSyncService dealSyncService,
    CancellationToken cancellationToken) =>
{
    var result = await dealSyncService.SyncPipelineDealsAsync(
        pipelineSlug,
        null,
        cancellationToken,
        SyncMode.Incremental);
    return Results.Ok(result);
});

app.MapPost("/api/bitrix/sync/webhook-pending", async (
    int? limit,
    IBitrixWebhookPendingSyncService webhookPendingSyncService,
    CancellationToken cancellationToken) =>
{
    var result = await webhookPendingSyncService.ProcessPendingDealChangesAsync(limit ?? 500, cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/bitrix/sync/reports/comercial/quick", async (
    string? pipeline,
    bool backfill,
    IServiceScopeFactory scopeFactory,
    CancellationToken cancellationToken) =>
{
    var allowedPipelines = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "rch_comercial", "pnnc_comercial" };
    var reportPipelines = string.IsNullOrWhiteSpace(pipeline)
        ? allowedPipelines.ToArray()
        : allowedPipelines.Contains(pipeline)
            ? new[] { pipeline }
            : Array.Empty<string>();
    if (reportPipelines.Length == 0)
    {
        return Results.BadRequest(new { message = "Pipeline no permitida para sincronizacion comercial rapida." });
    }

    var quickFields = new[]
    {
        "ID",
        "TITLE",
        "CATEGORY_ID",
        "STAGE_ID",
        "ASSIGNED_BY_ID",
        "OPPORTUNITY",
        "CURRENCY_ID",
        "DATE_CREATE",
        "DATE_MODIFY",
        "CLOSED",
        "UF_CRM_1676419915",
        "UF_CRM_1737653376"
    };
    var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["=UF_CRM_1737653376"] = "2026"
    };
    var stopwatch = Stopwatch.StartNew();

    var results = new List<SyncResult>();
    foreach (var pipelineSlug in reportPipelines)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dealSyncService = scope.ServiceProvider.GetRequiredService<IBitrixDealSyncService>();

        try
        {
            results.Add(await dealSyncService.SyncPipelineDealsAsync(
                pipelineSlug,
                null,
                cancellationToken,
                backfill ? SyncMode.Full : SyncMode.Incremental,
                fieldEqualsFilters: filters,
                selectFields: quickFields,
                reconcileMissing: false,
                entityTypeSuffix: "quick"));
        }
        catch (Exception exception)
        {
            results.Add(new SyncResult(
                Guid.Empty,
                $"deal:{pipelineSlug}:quick",
                "incremental",
                "failed",
                0,
                0,
                exception.Message));
        }
    }

    stopwatch.Stop();
    return Results.Ok(new
    {
        report = "comercial-quick",
        backfill,
        elapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
        recordsRead = results.Sum(result => result.RecordsRead),
        recordsWritten = results.Sum(result => result.RecordsWritten),
        results
    });
});

app.MapPost("/api/bitrix/sync/reports/comercial/commissions", async (
    int? year,
    IBitrixDealSyncService dealSyncService,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    var bogotaOffset = TimeSpan.FromHours(-5);
    var createdFrom = new DateTimeOffset(selectedYear, 1, 1, 0, 0, 0, bogotaOffset);
    var createdTo = new DateTimeOffset(selectedYear + 1, 1, 1, 0, 0, 0, bogotaOffset).AddTicks(-1);
    var fields = new[]
    {
        "ID",
        "TITLE",
        "CATEGORY_ID",
        "STAGE_ID",
        "ASSIGNED_BY_ID",
        "OPPORTUNITY",
        "CURRENCY_ID",
        "DATE_CREATE",
        "DATE_MODIFY",
        "CLOSED"
    };

    var result = await dealSyncService.SyncPipelineDealsAsync(
        "cuentas_cobro",
        null,
        cancellationToken,
        SyncMode.Full,
        createdFrom,
        createdTo,
        selectFields: fields,
        reconcileMissing: true,
        entityTypeSuffix: $"commissions:{selectedYear}",
        allowConcurrentWithOtherPipelines: true);

    return Results.Ok(result);
});

app.MapPost("/api/bitrix/sync/reports/comercial/portfolio-state", async (
    string? fromStageId,
    IBitrixDealSyncService dealSyncService,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var pipelineSlugs = new[]
    {
        "rch_cartera",
        "pnnc_cartera"
    };
    var stageNames = new[]
    {
        "VERIFICACION CASO EXITOSO",
        "CASOS CON NOVEDAD",
        "NOTIFICADO",
        "CARTERA EN TIEMPO",
        "MORA 0 - 30 DÍAS",
        "POR GESTIONAR",
        "ANTICIPO PENDIENTE RADICACIÓN",
        "CARTERA DE ESTRUCTURACIÓN",
        "EN SEGUIMIENTO",
        "MORA 30 - 60 DÍAS",
        "COBRO PRE JURIDICO",
        "ACUERDO DE PAGO",
        "INCUMPLIMIENTO DE ACUERDO",
        "COBRO JURIDICO",
        "OBJECIONES",
        "MORA 30 - 60 DIAS",
        "ACUERDO DE PAGO EN MORA",
        "GENERACIÓN DE PAZ Y SALVO",
        "GANADO",
        "GENERACIÓN PAZ Y SALVO"
    };
    var fields = new[]
    {
        "ID",
        "TITLE",
        "CATEGORY_ID",
        "STAGE_ID",
        "ASSIGNED_BY_ID",
        "OPPORTUNITY",
        "CURRENCY_ID",
        "DATE_CREATE",
        "DATE_MODIFY",
        "CLOSED"
    };
    const string stageSql = """
        SELECT pipeline.slug, stage.bitrix_stage_id
        FROM bitrix.pipeline_stages stage
        JOIN bitrix.pipelines pipeline ON pipeline.id = stage.pipeline_id
        WHERE pipeline.slug = ANY(@pipelineSlugs)
          AND UPPER(TRIM(stage.name)) = ANY(@stageNames)
        ORDER BY pipeline.sync_order, stage.sort_order, stage.bitrix_stage_id;
        """;
    var reportStages = new List<(string PipelineSlug, string StageId)>();
    await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
    await using (var command = new NpgsqlCommand(stageSql, connection))
    {
        command.Parameters.AddWithValue("pipelineSlugs", pipelineSlugs);
        command.Parameters.AddWithValue("stageNames", stageNames);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            reportStages.Add((reader.GetString(0), reader.GetString(1)));
        }
    }

    if (!string.IsNullOrWhiteSpace(fromStageId))
    {
        var resumeIndex = reportStages.FindIndex(stage =>
            string.Equals(stage.StageId, fromStageId, StringComparison.OrdinalIgnoreCase));
        if (resumeIndex < 0)
        {
            return Results.BadRequest(new { message = "La etapa indicada no pertenece al reporte Estado de cartera." });
        }

        reportStages = reportStages.Skip(resumeIndex).ToList();
    }

    var createdFrom = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var results = new List<SyncResult>();

    foreach (var (pipelineSlug, stageId) in reportStages)
    {
        var result = await dealSyncService.SyncPipelineDealsAsync(
            pipelineSlug,
            stageId,
            cancellationToken,
            SyncMode.Full,
            createdFrom,
            selectFields: fields,
            reconcileMissing: true,
            entityTypeSuffix: $"portfolio-state:{stageId}",
            allowConcurrentWithOtherPipelines: true);
        results.Add(result);

        if (!string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }
    }

    return Results.Ok(new
    {
        report = "portfolio-state",
        resumedFromStageId = fromStageId,
        status = reportStages.Count > 0
            && results.Count == reportStages.Count
            && results.All(result => string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            ? "succeeded"
            : "failed",
        stages = reportStages.Count,
        recordsRead = results.Sum(result => result.RecordsRead),
        recordsWritten = results.Sum(result => result.RecordsWritten),
        results
    });
});

app.MapPost("/api/bitrix/sync/reports/operativa/radicados", async (
    string pipeline,
    string stageId,
    IBitrixDealSyncService dealSyncService,
    CancellationToken cancellationToken) =>
{
    var allowedPipelines = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "rch_operativa",
        "pnnc_operativa",
        "1116_operativa",
        "lp_operativa_2445"
    };
    if (!allowedPipelines.Contains(pipeline) || string.IsNullOrWhiteSpace(stageId))
    {
        return Results.BadRequest(new { message = "Pipeline o etapa no permitida para sincronizacion operativa de radicados." });
    }

    var fields = new[]
    {
        "ID",
        "TITLE",
        "CATEGORY_ID",
        "STAGE_ID",
        "ASSIGNED_BY_ID",
        "OPPORTUNITY",
        "CURRENCY_ID",
        "DATE_CREATE",
        "DATE_MODIFY",
        "CLOSED",
        "UF_CRM_1676419915",
        "UF_CRM_1737653376"
    };
    var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["=UF_CRM_1737653376"] = "2026"
    };

    var result = await dealSyncService.SyncPipelineDealsAsync(
        pipeline,
        stageId,
        cancellationToken,
        SyncMode.Full,
        fieldEqualsFilters: filters,
        selectFields: fields,
        reconcileMissing: false,
        entityTypeSuffix: $"radicados:{stageId}");

    return Results.Ok(result);
});

app.MapPost("/api/bitrix/sync/massive", (
    IServiceScopeFactory scopeFactory) =>
{
    _ = Task.Run(async () =>
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var massiveSyncService = scope.ServiceProvider.GetRequiredService<IBitrixMassiveSyncService>();
        await massiveSyncService.SyncAllDealSummariesAsync(CancellationToken.None);
    });

    return Results.Accepted("/api/data/sync-state", new
    {
        status = "accepted",
        message = "Sincronizacion masiva iniciada en segundo plano."
    });
});

app.MapPost("/api/bitrix/sync/global", async (
    IBitrixSynchronizer synchronizer,
    CancellationToken cancellationToken) =>
{
    var results = await synchronizer.RunGlobalAsync(SyncMode.Full, cancellationToken);
    return Results.Accepted($"/api/bitrix/sync/global", results);
});

app.MapPost("/api/bitrix/sync/global/incremental", async (
    IBitrixSynchronizer synchronizer,
    CancellationToken cancellationToken) =>
{
    var results = await synchronizer.RunGlobalAsync(SyncMode.Incremental, cancellationToken);
    return Results.Accepted($"/api/bitrix/sync/global/incremental", results);
});

app.MapGet("/health/db", async (IDatabaseHealthCheck healthCheck, CancellationToken cancellationToken) =>
{
    await healthCheck.CheckAsync(cancellationToken);
    return Results.Ok(new { status = "ok" });
});

app.MapGet(
    "/reports/{reportDefinitionId:guid}/access/{userId:guid}",
    async (
        Guid reportDefinitionId,
        Guid userId,
        IReportAccessService reportAccessService,
        CancellationToken cancellationToken) =>
    {
        var allowed = await reportAccessService.UserCanAccessReportAsync(
            userId,
            reportDefinitionId,
            cancellationToken);

        return Results.Ok(new { allowed });
    });

var startupDataSource = app.Services.GetRequiredService<NpgsqlDataSource>();
await BitrixDataQueries.EnsureManagementPipelinesAsync(startupDataSource, CancellationToken.None);
await KnownBitrixCorrections.ApplyAsync(startupDataSource, CancellationToken.None);
await AdminAccessQueries.EnsureUserManagementSchemaAsync(startupDataSource, CancellationToken.None);
await AdminAccessQueries.EnsureReportCatalogAsync(startupDataSource, CancellationToken.None);
try
{
    await OrganizationQueries.EnsureSchemaAsync(startupDataSource, CancellationToken.None);
}
catch (Exception exception)
{
    app.Logger.LogError(
        exception,
        "The commercial organization schema could not be updated during startup. The panel will continue starting.");
}
await PanelAuthentication.EnsureSuperAdminAsync(
    app.Configuration["SUPERADMIN_EMAIL"] ?? "superadmin@avanzarsoluciones.com",
    app.Configuration["SUPERADMIN_PASSWORD"] ?? "AvanzarAdmin2026!",
    startupDataSource,
    CancellationToken.None);

var jobMode = builder.Configuration["BITRIX_SYNC_MODE"];
if (!string.IsNullOrWhiteSpace(jobMode))
{
    var normalizedJobMode = jobMode.Trim().ToLowerInvariant();
    if (normalizedJobMode == "webhook-pending")
    {
        var limit = builder.Configuration.GetValue<int?>("BITRIX_WEBHOOK_PENDING_LIMIT") ?? 500;
        app.Logger.LogInformation("Starting Bitrix webhook pending job with limit {Limit}.", limit);
        await using var webhookScope = app.Services.CreateAsyncScope();
        var pendingSyncService = webhookScope.ServiceProvider.GetRequiredService<IBitrixWebhookPendingSyncService>();
        var result = await pendingSyncService.ProcessPendingDealChangesAsync(limit, CancellationToken.None);

        app.Logger.LogInformation(
            "Bitrix webhook pending job result: {Status}, read {RecordsRead}, wrote {RecordsWritten}, error {ErrorMessage}",
            result.Status,
            result.RecordsRead,
            result.RecordsWritten,
            result.ErrorMessage);

        if (!string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = 1;
        }

        return;
    }

    if (normalizedJobMode == "commercial-nightly")
    {
        app.Logger.LogInformation("Starting Bitrix commercial nightly job.");
        await using var commercialScope = app.Services.CreateAsyncScope();
        var commercialSynchronizer = commercialScope.ServiceProvider.GetRequiredService<IBitrixSynchronizer>();
        var commercialResults = await commercialSynchronizer.RunCommercialNightlyAsync(CancellationToken.None);

        foreach (var result in commercialResults)
        {
            app.Logger.LogInformation(
                "Bitrix commercial nightly result: {EntityType} {Status}, read {RecordsRead}, wrote {RecordsWritten}, error {ErrorMessage}",
                result.EntityType,
                result.Status,
                result.RecordsRead,
                result.RecordsWritten,
                result.ErrorMessage);
        }

        if (commercialResults.Any(result => !string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.ExitCode = 1;
        }

        return;
    }

    var mode = normalizedJobMode switch
    {
        "full" => SyncMode.Full,
        "incremental" => SyncMode.Incremental,
        _ => throw new InvalidOperationException(
            "BITRIX_SYNC_MODE must be 'full', 'incremental', 'webhook-pending' or 'commercial-nightly'.")
    };

    app.Logger.LogInformation("Starting Bitrix synchronization job in {Mode} mode.", mode);
    await using var scope = app.Services.CreateAsyncScope();
    var synchronizer = scope.ServiceProvider.GetRequiredService<IBitrixSynchronizer>();
    var results = await synchronizer.RunGlobalAsync(mode, CancellationToken.None);

    foreach (var result in results)
    {
        app.Logger.LogInformation(
            "Bitrix job result: {EntityType} {Status}, read {RecordsRead}, wrote {RecordsWritten}, error {ErrorMessage}",
            result.EntityType,
            result.Status,
            result.RecordsRead,
            result.RecordsWritten,
            result.ErrorMessage);
    }

    if (results.Any(result => !string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase)))
    {
        Environment.ExitCode = 1;
    }

    return;
}

app.Run();

static string? TryGetHost(string? url)
{
    if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        return null;
    }

    return uri.Host;
}
