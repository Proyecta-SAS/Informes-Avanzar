namespace InformesAvanzar.Api.Sync;

public sealed class BitrixMassiveSyncService(
    IBitrixSyncRepository repository,
    IBitrixDealSyncService dealSyncService) : IBitrixMassiveSyncService
{
    public async Task<IReadOnlyList<SyncResult>> SyncAllDealSummariesAsync(CancellationToken cancellationToken)
    {
        var ownerId = $"{Environment.MachineName}:massive:{Guid.NewGuid():N}";
        var locked = await repository.TryAcquireGlobalLockAsync(ownerId, TimeSpan.FromHours(12), cancellationToken);

        if (!locked)
        {
            throw new InvalidOperationException("Ya hay una sincronizacion masiva activa.");
        }

        try
        {
            var pipelines = await repository.ListActivePipelinesAsync(cancellationToken);
            var results = new List<SyncResult>();

            foreach (var pipeline in pipelines.OrderBy(pipeline => pipeline.SyncOrder))
            {
                results.Add(await dealSyncService.SyncPipelineDealsAsync(pipeline.Slug, null, cancellationToken));
            }

            return results;
        }
        finally
        {
            await repository.ReleaseGlobalLockAsync(ownerId, cancellationToken);
        }
    }
}
