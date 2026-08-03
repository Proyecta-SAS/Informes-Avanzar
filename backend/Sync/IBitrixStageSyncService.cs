namespace InformesAvanzar.Api.Sync;

public interface IBitrixStageSyncService
{
    Task<SyncResult> SyncStagesAsync(CancellationToken cancellationToken);
}
