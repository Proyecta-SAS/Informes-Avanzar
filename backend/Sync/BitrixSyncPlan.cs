using InformesAvanzar.Api.Bitrix;

namespace InformesAvanzar.Api.Sync;

public sealed record BitrixSyncPlan(
    SyncMode Mode,
    IReadOnlyList<BitrixPipeline> Pipelines,
    DateTimeOffset? Since);
