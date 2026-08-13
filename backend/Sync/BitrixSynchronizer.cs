using InformesAvanzar.Api.Bitrix;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixSynchronizer(
    IBitrixSyncRepository repository,
    IBitrixUserSyncService userSyncService,
    IBitrixStageSyncService stageSyncService,
    IBitrixDealSyncService dealSyncService,
    IBitrixActivitySyncService activitySyncService) : IBitrixSynchronizer
{
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

            return results;
        }
        finally
        {
            await repository.ReleaseGlobalLockAsync(ownerId, CancellationToken.None);
        }
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

}
