using InformesAvanzar.Api.Bitrix;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixSynchronizer(
    IBitrixClient bitrixClient,
    IBitrixSyncRepository repository) : IBitrixSynchronizer
{
    public async Task<IReadOnlyList<SyncResult>> RunGlobalAsync(SyncMode mode, CancellationToken cancellationToken)
    {
        var ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var locked = await repository.TryAcquireGlobalLockAsync(ownerId, TimeSpan.FromHours(2), cancellationToken);

        if (!locked)
        {
            throw new InvalidOperationException("Another global Bitrix synchronization is already running.");
        }

        try
        {
            var connection = await repository.GetActiveConnectionAsync(cancellationToken);
            var pipelines = await repository.ListActivePipelinesAsync(cancellationToken);
            var results = new List<SyncResult>();

            results.Add(await RunEntityAsync(connection.Id, "user", mode, SyncUsersAsync, cancellationToken));

            foreach (var pipeline in pipelines.OrderBy(p => p.SyncOrder))
            {
                results.Add(await RunPipelineAsync(connection.Id, pipeline, mode, cancellationToken));
            }

            return results;
        }
        finally
        {
            await repository.ReleaseGlobalLockAsync(ownerId, cancellationToken);
        }
    }

    private async Task<SyncResult> RunPipelineAsync(
        Guid connectionId,
        BitrixPipeline pipeline,
        SyncMode mode,
        CancellationToken cancellationToken)
    {
        var entityType = $"deal:{pipeline.Slug}";
        return await RunEntityAsync(
            connectionId,
            entityType,
            mode,
            async token =>
            {
                await bitrixClient.CallAsync(
                    BitrixMethod.DealCategoryStageList,
                    new Dictionary<string, string>
                    {
                        ["id"] = pipeline.CategoryId.ToString()
                    },
                    token);

                var parameters = new Dictionary<string, string>
                {
                    ["filter[CATEGORY_ID]"] = pipeline.CategoryId.ToString(),
                    ["select[]"] = "*"
                };

                await bitrixClient.CallAsync(BitrixMethod.DealList, parameters, token);
                return new SyncEntityCount(0, 0);
            },
            cancellationToken);
    }

    private async Task<SyncEntityCount> SyncUsersAsync(CancellationToken cancellationToken)
    {
        await bitrixClient.CallAsync(BitrixMethod.UserGet, new Dictionary<string, string>(), cancellationToken);
        return new SyncEntityCount(0, 0);
    }

    private async Task<SyncResult> RunEntityAsync(
        Guid connectionId,
        string entityType,
        SyncMode mode,
        Func<CancellationToken, Task<SyncEntityCount>> sync,
        CancellationToken cancellationToken)
    {
        var syncRunId = await repository.StartRunAsync(connectionId, entityType, mode, cancellationToken);

        try
        {
            var count = await sync(cancellationToken);
            await repository.FinishRunAsync(syncRunId, "succeeded", count.RecordsRead, count.RecordsWritten, null, cancellationToken);
            return new SyncResult(syncRunId, entityType, ToDbMode(mode), "succeeded", count.RecordsRead, count.RecordsWritten);
        }
        catch (Exception ex)
        {
            await repository.FinishRunAsync(syncRunId, "failed", 0, 0, ex.Message, cancellationToken);
            return new SyncResult(syncRunId, entityType, ToDbMode(mode), "failed", 0, 0, ex.Message);
        }
    }

    private static string ToDbMode(SyncMode mode) => mode switch
    {
        SyncMode.Full => "full",
        SyncMode.Incremental => "incremental",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private sealed record SyncEntityCount(int RecordsRead, int RecordsWritten);
}
