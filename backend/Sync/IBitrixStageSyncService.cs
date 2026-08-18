namespace InformesAvanzar.Api.Sync;

public interface IBitrixStageSyncService
{
    Task<SyncResult> SyncStagesAsync(CancellationToken cancellationToken);

    Task<SyncResult> SyncPipelineStagesAsync(
        string pipelineSlug,
        IReadOnlySet<string> stageNames,
        CancellationToken cancellationToken);
}
