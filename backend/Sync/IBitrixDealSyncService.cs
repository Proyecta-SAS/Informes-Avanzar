namespace InformesAvanzar.Api.Sync;

public interface IBitrixDealSyncService
{
    Task<SyncResult> SyncPipelineDealsAsync(string pipelineSlug, string? stageId, CancellationToken cancellationToken);
}
