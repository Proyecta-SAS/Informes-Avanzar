namespace InformesAvanzar.Api.Sync;

public interface IBitrixDealSyncService
{
    Task<SyncResult> SyncDealByIdAsync(
        string bitrixId,
        Guid? existingSyncRunId,
        CancellationToken cancellationToken);

    Task<SyncResult> SyncPipelineDealsAsync(
        string pipelineSlug,
        string? stageId,
        CancellationToken cancellationToken,
        SyncMode mode = SyncMode.Full,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        IReadOnlyDictionary<string, string>? fieldEqualsFilters = null,
        IReadOnlyList<string>? selectFields = null,
        bool reconcileMissing = true,
        string? entityTypeSuffix = null);
}
