namespace InformesAvanzar.Api.Sync;

public interface IBitrixSynchronizer
{
    Task<IReadOnlyList<SyncResult>> RunGlobalAsync(SyncMode mode, CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncResult>> RunCommercialNightlyAsync(CancellationToken cancellationToken);
}
