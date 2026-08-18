using InformesAvanzar.Api.Bitrix;
using Npgsql;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixSynchronizer(
    IBitrixSyncRepository repository,
    IBitrixUserSyncService userSyncService,
    IBitrixStageSyncService stageSyncService,
    IBitrixDealSyncService dealSyncService,
    IBitrixActivitySyncService activitySyncService,
    NpgsqlDataSource dataSource) : IBitrixSynchronizer
{
    private const string MetaGoalsPipelineSlug = "informes_bi_builder";
    private static readonly string[] MetaGoalStageNames =
    [
        "Meta RCH Coordinadores",
        "Meta INS Coordinadores",
        "Meta 1116 Coordinadores"
    ];
    private static readonly string[] MetaGoalFields =
    [
        "ID",
        "TITLE",
        "CATEGORY_ID",
        "STAGE_ID",
        "ASSIGNED_BY_ID",
        "ASSIGNED_BY_NAME",
        "ASSIGNED_BY_DEPARTMENT",
        "OPPORTUNITY",
        "CURRENCY_ID",
        "DATE_CREATE",
        "DATE_MODIFY",
        "CLOSED",
        "UF_CRM_1611163412",
        "UF_CRM_1737653376"
    ];

    public async Task<IReadOnlyList<SyncResult>> RunGlobalAsync(SyncMode mode, CancellationToken cancellationToken)
    {
        var ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var lockTtl = mode == SyncMode.Full
            ? TimeSpan.FromHours(20)
            : TimeSpan.FromHours(2);
        var locked = await repository.TryAcquireGlobalLockAsync(ownerId, lockTtl, cancellationToken);

        if (!locked)
        {
            throw new SyncAlreadyRunningException("Ya hay una sincronizacion global de Bitrix activa.");
        }

        try
        {
            var pipelines = await repository.ListActivePipelinesAsync(cancellationToken);
            var results = new List<SyncResult>();

            if (mode == SyncMode.Full)
            {
                results.Add(await userSyncService.SyncUsersAsync(cancellationToken));
                results.Add(await stageSyncService.SyncStagesAsync(cancellationToken));
            }

            foreach (var pipeline in pipelines.OrderBy(p => p.SyncOrder))
            {
                results.Add(await dealSyncService.SyncPipelineDealsAsync(
                    pipeline.Slug,
                    null,
                    cancellationToken,
                    mode));
            }

            results.Add(await activitySyncService.SyncActivitiesAsync(cancellationToken));

            return results;
        }
        finally
        {
            await repository.ReleaseGlobalLockAsync(ownerId, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<SyncResult>> RunCommercialNightlyAsync(CancellationToken cancellationToken)
    {
        var ownerId = $"{Environment.MachineName}:commercial-nightly:{Guid.NewGuid():N}";
        var locked = await repository.TryAcquireGlobalLockAsync(ownerId, TimeSpan.FromHours(8), cancellationToken);

        if (!locked)
        {
            throw new InvalidOperationException("Another global Bitrix synchronization is already running.");
        }

        try
        {
            var reportYear = GetCommercialSyncYear();
            var createdFrom = new DateTimeOffset(reportYear, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));
            var createdTo = createdFrom.AddYears(1).AddTicks(-1);
            var results = new List<SyncResult>();

            foreach (var pipelineSlug in new[] { "rch_comercial", "pnnc_comercial" })
            {
                results.AddRange(await SyncPipelineWithTransientRetriesAsync(
                    pipelineSlug,
                    cancellationToken,
                    createdFrom,
                    createdTo,
                    reconcileMissing: false,
                    entityTypeSuffix: $"nightly-{reportYear}"));
            }

            foreach (var pipelineSlug in new[] { "rch_operativa", "pnnc_operativa" })
            {
                results.AddRange(await SyncPipelineWithTransientRetriesAsync(
                    pipelineSlug,
                    cancellationToken,
                    reconcileMissing: true));
            }

            results.AddRange(await RunCommercialMetaCoordinatorGoalsAsync(reportYear, cancellationToken));

            return results;
        }
        finally
        {
            await repository.ReleaseGlobalLockAsync(ownerId, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<SyncResult>> RunCommercialMetaCoordinatorGoalsAsync(
        int year,
        CancellationToken cancellationToken)
    {
        var selectedYear = NormalizeReportYear(year);
        var results = new List<SyncResult>();

        var stageResult = await stageSyncService.SyncPipelineStagesAsync(
            MetaGoalsPipelineSlug,
            MetaGoalStageNames.ToHashSet(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        results.Add(stageResult);

        if (!string.Equals(stageResult.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return results;
        }

        var reportStages = await ListMetaGoalStageIdsAsync(cancellationToken);
        if (reportStages.Count == 0)
        {
            results.Add(new SyncResult(
                Guid.Empty,
                $"deal:{MetaGoalsPipelineSlug}:meta-coordinadores:{selectedYear}",
                "full",
                "failed",
                stageResult.RecordsRead,
                0,
                "No se encontraron las etapas Meta RCH Coordinadores, Meta INS Coordinadores y Meta 1116 Coordinadores en la pipeline 224."));
            return results;
        }

        var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["=UF_CRM_1737653376"] = GetMetaGoalYearFilterValue(selectedYear)
        };

        foreach (var stageId in reportStages)
        {
            results.Add(await dealSyncService.SyncPipelineDealsAsync(
                MetaGoalsPipelineSlug,
                stageId,
                cancellationToken,
                SyncMode.Full,
                fieldEqualsFilters: filters,
                selectFields: MetaGoalFields,
                reconcileMissing: false,
                entityTypeSuffix: $"meta-coordinadores:{selectedYear}:{stageId}",
                allowConcurrentWithOtherPipelines: true));
        }

        return results;
    }

    private async Task<IReadOnlyList<string>> ListMetaGoalStageIdsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT stage.bitrix_stage_id
            FROM bitrix.pipeline_stages stage
            JOIN bitrix.pipelines pipeline ON pipeline.id = stage.pipeline_id
            WHERE pipeline.slug = @pipelineSlug
              AND UPPER(TRIM(stage.name)) = ANY(@stageNames)
            ORDER BY stage.sort_order, stage.bitrix_stage_id;
            """;

        var stageIds = new List<string>();
        var stageNames = MetaGoalStageNames
            .Select(stage => stage.Trim().ToUpperInvariant())
            .ToArray();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("pipelineSlug", MetaGoalsPipelineSlug);
        command.Parameters.AddWithValue("stageNames", stageNames);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            stageIds.Add(reader.GetString(0));
        }

        return stageIds;
    }

    private async Task<IReadOnlyList<SyncResult>> SyncPipelineWithTransientRetriesAsync(
        string pipelineSlug,
        CancellationToken cancellationToken,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        bool reconcileMissing = true,
        string? entityTypeSuffix = null)
    {
        var results = new List<SyncResult>();
        var startAt = 0;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var result = await dealSyncService.SyncPipelineDealsAsync(
                pipelineSlug,
                null,
                cancellationToken,
                SyncMode.Full,
                createdFrom: createdFrom,
                createdTo: createdTo,
                reconcileMissing: reconcileMissing,
                entityTypeSuffix: entityTypeSuffix,
                allowConcurrentWithOtherPipelines: true,
                startAt: startAt);

            results.Add(result);
            if (result.Status == "succeeded" || !IsTransientSyncError(result.ErrorMessage) || result.RecordsRead <= startAt)
            {
                return results;
            }

            startAt = reconcileMissing
                ? 0
                : Math.Max(startAt, result.RecordsRead / 50 * 50);
            await Task.Delay(TimeSpan.FromSeconds(15 * attempt), cancellationToken);
        }

        return results;
    }

    private static bool IsTransientSyncError(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        return errorMessage.Contains("Exception while reading from stream", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("temporarily", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetCommercialSyncYear()
    {
        var configuredYear = Environment.GetEnvironmentVariable("BITRIX_COMMERCIAL_SYNC_YEAR");
        if (int.TryParse(configuredYear, out var parsedYear) && parsedYear is >= 2020 and <= 2100)
        {
            return parsedYear;
        }

        return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).Year;
    }

    private static int NormalizeReportYear(int year) =>
        year is >= 2020 and <= 2100
            ? year
            : DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-5)).Year;

    private static string GetMetaGoalYearFilterValue(int year) =>
        // Bitrix stores the custom year list by option id in the REST API.
        year == 2026 ? "39138" : year.ToString(System.Globalization.CultureInfo.InvariantCulture);

}
