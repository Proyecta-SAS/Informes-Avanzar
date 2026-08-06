namespace InformesAvanzar.Api.Sync;

public interface IBitrixActivitySyncService
{
    Task<SyncResult> SyncActivitiesAsync(CancellationToken cancellationToken);
}
