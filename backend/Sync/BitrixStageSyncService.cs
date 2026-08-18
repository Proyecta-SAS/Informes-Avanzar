using System.Text.Json;
using InformesAvanzar.Api.Bitrix;
using Npgsql;
using NpgsqlTypes;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixStageSyncService(
    IBitrixClient bitrixClient,
    IBitrixSyncRepository repository,
    NpgsqlDataSource dataSource) : IBitrixStageSyncService
{
    public async Task<SyncResult> SyncStagesAsync(CancellationToken cancellationToken)
        => await SyncStagesInternalAsync(null, null, "deal_category_stage", null, cancellationToken);

    public async Task<SyncResult> SyncPipelineStagesAsync(
        string pipelineSlug,
        IReadOnlySet<string> stageNames,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pipelineSlug))
        {
            throw new ArgumentException("Pipeline slug is required.", nameof(pipelineSlug));
        }

        if (stageNames.Count == 0)
        {
            throw new ArgumentException("At least one stage name is required.", nameof(stageNames));
        }

        var normalizedStageNames = stageNames
            .Select(NormalizeStageName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return await SyncStagesInternalAsync(
            pipelineSlug,
            normalizedStageNames,
            $"deal_category_stage:{pipelineSlug}",
            $"deal_category_stage:{pipelineSlug}",
            cancellationToken);
    }

    private async Task<SyncResult> SyncStagesInternalAsync(
        string? pipelineSlug,
        IReadOnlySet<string>? normalizedStageNames,
        string entityType,
        string? concurrencyKey,
        CancellationToken cancellationToken)
    {
        var connectionInfo = await repository.GetActiveConnectionAsync(cancellationToken);
        var syncRunId = await repository.StartRunAsync(connectionInfo.Id, entityType, SyncMode.Full, cancellationToken, concurrencyKey);

        var recordsRead = 0;
        var recordsWritten = 0;

        try
        {
            var pipelines = await ListPipelinesAsync(pipelineSlug, cancellationToken);

            foreach (var pipeline in pipelines)
            {
                using var response = await bitrixClient.CallAsync(
                    BitrixMethod.DealCategoryStageList,
                    new[]
                    {
                        new KeyValuePair<string, string>("id", pipeline.CategoryId.ToString())
                    },
                    cancellationToken);

                var root = response.RootElement;

                if (root.TryGetProperty("error", out var error))
                {
                    throw new InvalidOperationException(error.GetString());
                }

                if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var stage in result.EnumerateArray())
                {
                    recordsRead++;
                    var stageName = GetString(stage, "NAME") ?? GetString(stage, "STATUS_ID") ?? GetString(stage, "ID") ?? string.Empty;
                    if (normalizedStageNames is not null && !normalizedStageNames.Contains(NormalizeStageName(stageName)))
                    {
                        continue;
                    }

                    await UpsertStageAsync(pipeline.Id, stage, cancellationToken);
                    recordsWritten++;
                }
            }

            await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, CancellationToken.None);
            return new SyncResult(syncRunId, entityType, "full", "succeeded", recordsRead, recordsWritten);
        }
        catch (Exception ex)
        {
            await repository.FinishRunAsync(syncRunId, "failed", recordsRead, recordsWritten, ex.Message, CancellationToken.None);
            return new SyncResult(syncRunId, entityType, "full", "failed", recordsRead, recordsWritten, ex.Message);
        }
    }

    private async Task<IReadOnlyList<PipelineRecord>> ListPipelinesAsync(string? pipelineSlug, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, slug, name, category_id
            FROM bitrix.pipelines
            WHERE is_active = true
              AND (@pipelineSlug IS NULL OR slug = @pipelineSlug)
            ORDER BY sync_order, category_id;
            """;

        var pipelines = new List<PipelineRecord>();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("pipelineSlug", NpgsqlDbType.Text).Value = (object?)pipelineSlug ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            pipelines.Add(new PipelineRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return pipelines;
    }

    private async Task UpsertStageAsync(Guid pipelineId, JsonElement stage, CancellationToken cancellationToken)
    {
        var stageId = GetString(stage, "STATUS_ID") ?? GetString(stage, "ID");

        if (string.IsNullOrWhiteSpace(stageId))
        {
            throw new InvalidOperationException("Bitrix stage without STATUS_ID or ID.");
        }

        const string sql = """
            INSERT INTO bitrix.pipeline_stages (
                pipeline_id,
                bitrix_stage_id,
                name,
                sort_order,
                status_type,
                raw_payload
            )
            VALUES (
                @pipelineId,
                @bitrixStageId,
                @name,
                @sortOrder,
                @statusType,
                @rawPayload
            )
            ON CONFLICT (pipeline_id, bitrix_stage_id) DO UPDATE
            SET name = EXCLUDED.name,
                sort_order = EXCLUDED.sort_order,
                status_type = EXCLUDED.status_type,
                raw_payload = EXCLUDED.raw_payload;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("pipelineId", pipelineId);
        command.Parameters.AddWithValue("bitrixStageId", stageId);
        command.Parameters.AddWithValue("name", GetString(stage, "NAME") ?? stageId);
        command.Parameters.AddWithValue("sortOrder", (object?)GetInt(stage, "SORT") ?? DBNull.Value);
        command.Parameters.AddWithValue("statusType", (object?)GetString(stage, "SEMANTICS") ?? DBNull.Value);
        command.Parameters.Add("rawPayload", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(stage);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString()
            : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeStageName(string value) => value.Trim().ToUpperInvariant();

    private sealed record PipelineRecord(Guid Id, string Slug, string Name, int CategoryId);
}
