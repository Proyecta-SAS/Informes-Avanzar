namespace InformesAvanzar.Api.Sync;

public interface IBitrixMassiveSyncService
{
    Task<IReadOnlyList<SyncResult>> SyncAllDealSummariesAsync(CancellationToken cancellationToken);
}
