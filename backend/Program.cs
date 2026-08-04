using InformesAvanzar.Api.Bitrix;
using InformesAvanzar.Api.Auth;
using System.Text.Json;
using InformesAvanzar.Api.Configuration;
using InformesAvanzar.Api.Data;
using InformesAvanzar.Api.Reports;
using InformesAvanzar.Api.Sync;
using Npgsql;

EnvFileLoader.LoadNearest(".env");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.Configure<BitrixOptions>(builder.Configuration.GetSection("Bitrix"));
builder.Services.PostConfigure<BitrixOptions>(options =>
{
    options.WebhookUrl = builder.Configuration["BITRIX_WEBHOOK_URL"] ?? options.WebhookUrl;
});
builder.Services.AddHttpClient<IBitrixClient, BitrixClient>();

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
builder.Services.AddScoped<IBitrixMassiveSyncService, BitrixMassiveSyncService>();

var app = builder.Build();

var panelSigningKey = app.Configuration["PANEL_AUTH_SIGNING_KEY"] ?? app.Configuration["ADMIN_API_KEY"] ?? throw new InvalidOperationException("Configure PANEL_AUTH_SIGNING_KEY o ADMIN_API_KEY.");
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "/";
    var isPublic = path is "/login.html" or "/login.js" or "/health" or "/health/db" || path.StartsWith("/assets/") || path == "/styles.css" || path == "/api/auth/login";
    var panelUser = PanelAuthentication.ValidateToken(context.Request.Cookies[PanelAuthentication.CookieName], panelSigningKey);
    if (panelUser is not null) context.Items["PanelUser"] = panelUser;
    if (!isPublic && panelUser is null)
    {
        if (path.StartsWith("/api/")) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        context.Response.Redirect($"/login.html?returnUrl={Uri.EscapeDataString(path)}");
        return;
    }
    if (path == "/login.html" && panelUser is not null) { context.Response.Redirect("/"); return; }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

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

app.MapGet("/api/auth/me", (HttpContext context) => Results.Ok(context.Items["PanelUser"]));

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

var adminApi = app.MapGroup("/api/admin");
adminApi.AddEndpointFilter(async (context, next) =>
{
    if (context.HttpContext.Items["PanelUser"] is PanelUser { RoleCode: "admin" }) return await next(context);
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

app.MapGet("/api/reports/fuerza-comercial-diego/valores-radicados", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoRadicatedValuesAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/fuerza-comercial-diego/dashboard", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoCommercialDashboardAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/fuerza-comercial-diego/cartera-recaudada", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoPortfolioCollectionsAsync(
        selectedYear,
        dataSource,
        cancellationToken));
});

app.MapGet("/api/reports/fuerza-comercial-diego/liderazgo-comisiones", async (
    int? year,
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    var selectedYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.UtcNow.Year;
    return Results.Ok(await BitrixDataQueries.GetDiegoLeadershipAndCommissionsAsync(
        selectedYear,
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
        webhookHost = TryGetHost(webhookUrl),
        scopes = options.Value.Scopes
    });
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
    using var response = await bitrixClient.CallAsync(
        "department.get",
        Array.Empty<KeyValuePair<string, string>>(),
        cancellationToken);

    if (response.RootElement.TryGetProperty("error", out var error))
    {
        return Results.BadRequest(new { status = "failed", error = error.GetString() });
    }

    if (!response.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
    {
        return Results.Ok(new { status = "succeeded", recordsWritten = 0 });
    }

    var departments = result.EnumerateArray()
        .Select(item => new
        {
            Id = long.Parse(item.GetProperty("ID").ToString()),
            Name = item.TryGetProperty("NAME", out var name) ? name.ToString() : "Departamento",
            ParentId = item.TryGetProperty("PARENT", out var parent)
                && long.TryParse(parent.ToString(), out var parentId)
                && parentId > 0 ? parentId : (long?)null,
            Sort = item.TryGetProperty("SORT", out var sort)
                && int.TryParse(sort.ToString(), out var sortOrder) ? sortOrder : 0
        })
        .ToArray();

    await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

    foreach (var department in departments)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO bitrix.departments (id, name, parent_id, sort_order, updated_at)
            VALUES (@id, @name, NULL, @sort, now())
            ON CONFLICT (id) DO UPDATE
            SET name = EXCLUDED.name,
                sort_order = EXCLUDED.sort_order,
                updated_at = now();
            """, connection, transaction);
        command.Parameters.AddWithValue("id", department.Id);
        command.Parameters.AddWithValue("name", department.Name);
        command.Parameters.AddWithValue("sort", department.Sort);
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
    return Results.Ok(new { status = "succeeded", recordsWritten = departments.Length });
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
    IBitrixDealSyncService dealSyncService,
    CancellationToken cancellationToken) =>
{
    var result = await dealSyncService.SyncPipelineDealsAsync(pipelineSlug, stageId, cancellationToken);
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
await AdminAccessQueries.EnsureUserManagementSchemaAsync(startupDataSource, CancellationToken.None);
await AdminAccessQueries.EnsureReportCatalogAsync(startupDataSource, CancellationToken.None);
await PanelAuthentication.EnsureSuperAdminAsync(
    app.Configuration["SUPERADMIN_EMAIL"] ?? "superadmin@avanzarsoluciones.com",
    app.Configuration["SUPERADMIN_PASSWORD"] ?? "AvanzarAdmin2026!",
    startupDataSource,
    CancellationToken.None);

var jobMode = builder.Configuration["BITRIX_SYNC_MODE"];
if (!string.IsNullOrWhiteSpace(jobMode))
{
    var mode = jobMode.Trim().ToLowerInvariant() switch
    {
        "full" => SyncMode.Full,
        "incremental" => SyncMode.Incremental,
        _ => throw new InvalidOperationException(
            "BITRIX_SYNC_MODE must be either 'full' or 'incremental'.")
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
