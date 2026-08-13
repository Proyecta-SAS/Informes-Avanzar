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
                results.Add(await dealSyncService.SyncPipelineDealsAsync(
                    pipelineSlug,
                    null,
                    cancellationToken,
                    SyncMode.Full,
                    createdFrom: createdFrom,
                    createdTo: createdTo,
                    reconcileMissing: false,
                    entityTypeSuffix: $"nightly-{reportYear}",
                    allowConcurrentWithOtherPipelines: true));
            }

            foreach (var pipelineSlug in new[] { "rch_operativa", "pnnc_operativa" })
            {
                results.Add(await dealSyncService.SyncPipelineDealsAsync(
                    pipelineSlug,
                    null,
                    cancellationToken,
                    SyncMode.Full,
                    allowConcurrentWithOtherPipelines: true));
            }

            return results;
        }
        finally
        {
            await repository.ReleaseGlobalLockAsync(ownerId, CancellationToken.None);
        }
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
