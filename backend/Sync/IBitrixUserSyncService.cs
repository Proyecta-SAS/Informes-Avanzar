namespace InformesAvanzar.Api.Sync;

public interface IBitrixUserSyncService
{
    Task<SyncResult> SyncUsersAsync(CancellationToken cancellationToken);
}
