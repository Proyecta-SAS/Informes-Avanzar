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

}
