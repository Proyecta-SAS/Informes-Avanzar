using InformesAvanzar.Api.Bitrix;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixSynchronizer(
    IBitrixSyncRepository repository,
    IBitrixUserSyncService userSyncService,
    IBitrixStageSyncService stageSyncService,
    IBitrixDealSyncService dealSyncService) : IBitrixSynchronizer
{
    public async Task<IReadOnlyList<SyncResult>> RunGlobalAsync(SyncMode mode, CancellationToken cancellationToken)
    {
        var ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var locked = await repository.TryAcquireGlobalLockAsync(ownerId, TimeSpan.FromHours(2), cancellationToken);

        if (!locked)
        {
            throw new InvalidOperationException("Another global Bitrix synchronization is already running.");
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

            return results;
        }
        finally
        {
            await repository.ReleaseGlobalLockAsync(ownerId, cancellationToken);
        }
    }

}
