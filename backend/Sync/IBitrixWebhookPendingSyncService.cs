namespace InformesAvanzar.Api.Sync;

public interface IBitrixWebhookPendingSyncService
{
    Task<SyncResult> ProcessPendingDealChangesAsync(int limit, CancellationToken cancellationToken);
}
