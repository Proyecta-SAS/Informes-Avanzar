namespace InformesAvanzar.Api.Sync;

public interface IBitrixDealSyncService
{
    Task<SyncResult> SyncPipelineDealsAsync(string pipelineSlug, CancellationToken cancellationToken);
}
