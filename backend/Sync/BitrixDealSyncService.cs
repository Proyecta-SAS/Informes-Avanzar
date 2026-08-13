using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InformesAvanzar.Api.Bitrix;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace InformesAvanzar.Api.Sync;

public sealed class BitrixDealSyncService(
    IBitrixClient bitrixClient,
    IBitrixSyncRepository repository,
    NpgsqlDataSource dataSource,
    IOptions<BitrixOptions> options) : IBitrixDealSyncService
{
    private readonly BitrixOptions _options = options.Value;
    private static readonly DateTimeOffset FullSyncCreatedFrom =
        new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Bitrix requires UF_* explicitly; '*' alone only returns standard deal fields.
    // The general commercial report depends on payment, hierarchy and period fields,
    // so every synchronization must preserve the complete custom-field payload.
    private static readonly string[] DealSummaryFields =
    [
        "*",
        "UF_*",
        // Bitrix can omit category-specific fields from UF_*; keep report-critical
        // fields explicit so period and month filters remain complete.
        "UF_CRM_1676419915",
        "UF_CRM_1737653376",
        "UF_CRM_1737654190",
        "UF_CRM_1628266904138",
        "UF_CRM_1628267643816",
        "UF_CRM_1584496415",
        "UF_CRM_1654544609940",
        "UF_CRM_1590601503",
        "UF_CRM_1597419361",
        "UF_CRM_1647621561172",
        "UF_CRM_1704395777981",
        "UF_CRM_1597419674",
        "UF_CRM_1597419734",
        "UF_CRM_1651070289441",
        "UF_CRM_1651008361471",
        "UF_CRM_1705001523",
        "UF_CRM_1628266963127",
        "UF_CRM_1654544553289",
        "UF_CRM_1628267025513",
        "UF_CRM_1628267431791",
        "UF_CRM_1628267476819",
        "UF_CRM_1628267492835",
        "UF_CRM_1623300067103",
        "UF_CRM_1628267593088",
        "UF_CRM_1628267618395",
        "UF_CRM_1746737855366",
        "UF_CRM_1746737634803",
        "UF_CRM_1747243052183",
        "UF_CRM_1746189764",
        "UF_CRM_1623281084711",
        "UF_CRM_1601670888849",
        "UF_CRM_1676487033939",
        "UF_CRM_1601671184915",
        "UF_CRM_1676486744268",
        "UF_CRM_1676486694789",
        "UF_CRM_1601671494647",
        "UF_CRM_1616543806805",
        "UF_CRM_1601671936104",
        "UF_CRM_1616543676428",
        "UF_CRM_1601672168191",
        "UF_CRM_1614529675",
        "UF_CRM_1614529793",
        "UF_CRM_1694637524493",
        "UF_CRM_1607022885798",
        "UF_CRM_1690228297166",
        "UF_CRM_1721937687",
        "UF_CRM_1592275850",
        "UF_CRM_1754067079",
        "UF_CRM_1611163473",
        "UF_CRM_1584482115955"
    ];

    public async Task<SyncResult> SyncDealByIdAsync(
        string bitrixId,
        Guid? existingSyncRunId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bitrixId))
        {
            throw new ArgumentException("Bitrix deal ID is required.", nameof(bitrixId));
        }

        var connectionInfo = await repository.GetActiveConnectionAsync(cancellationToken);
        var syncRunId = existingSyncRunId ?? await repository.StartRunAsync(connectionInfo.Id, "deal:webhook", SyncMode.Incremental, cancellationToken);
        var recordsRead = 0;
        var recordsWritten = 0;

        try
        {
            using var response = await bitrixClient.CallAsync(
                BitrixMethod.DealGet,
                new[] { new KeyValuePair<string, string>("id", bitrixId) },
                cancellationToken);

            var root = response.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException(error.GetString());
            }
            if (!root.TryGetProperty("result", out var deal) || deal.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Bitrix did not return deal {bitrixId}.");
            }

            recordsRead = 1;
            var categoryId = GetString(deal, "CATEGORY_ID")
                ?? throw new InvalidOperationException($"Bitrix deal {bitrixId} has no CATEGORY_ID.");
            var pipeline = await GetPipelineByCategoryIdAsync(categoryId, cancellationToken);
            if (pipeline is null)
            {
                // Outgoing webhooks are configured at the Bitrix portal level, so
                // events can arrive for pipelines that this application does not
                // synchronize. Treat those events as successfully ignored instead
                // of retrying them as failures.
                if (existingSyncRunId is null)
                {
                    await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, CancellationToken.None);
                }

                return new SyncResult(syncRunId, "deal:webhook", "incremental", "succeeded", recordsRead, recordsWritten);
            }

            if (!PipelineDomainIsAllowed(pipeline.Domain))
            {
                if (existingSyncRunId is null)
                {
                    await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, CancellationToken.None);
                }

                return new SyncResult(syncRunId, "deal:webhook", "incremental", "succeeded", recordsRead, recordsWritten);
            }

            await using var dbConnection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);
            var currentHashes = await LoadCurrentDealHashesAsync(
                dbConnection,
                transaction,
                connectionInfo.Id,
                [bitrixId],
                cancellationToken);

            if (await UpsertDealAsync(
                dbConnection,
                transaction,
                connectionInfo.Id,
                pipeline.Id,
                syncRunId,
                deal,
                bitrixId,
                currentHashes.GetValueOrDefault(bitrixId),
                cancellationToken,
                persistUnchangedPayload: true))
            {
                recordsWritten = 1;
            }

            await transaction.CommitAsync(cancellationToken);
            var cursor = GetDateTimeOffset(deal, "DATE_MODIFY");
            if (existingSyncRunId is null)
            {
                await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, CancellationToken.None, cursor);
            }

            return new SyncResult(syncRunId, "deal:webhook", "incremental", "succeeded", recordsRead, recordsWritten);
        }
        catch (Exception ex)
        {
            if (existingSyncRunId is null)
            {
                await repository.FinishRunAsync(syncRunId, "failed", recordsRead, recordsWritten, ex.Message, CancellationToken.None);
            }

            return new SyncResult(syncRunId, "deal:webhook", "incremental", "failed", recordsRead, recordsWritten, ex.Message);
        }
    }

    public async Task<SyncResult> SyncPipelineDealsAsync(
        string pipelineSlug,
        string? stageId,
        CancellationToken cancellationToken,
        SyncMode mode = SyncMode.Full,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        IReadOnlyDictionary<string, string>? fieldEqualsFilters = null,
        IReadOnlyList<string>? selectFields = null,
        bool reconcileMissing = true,
        string? entityTypeSuffix = null)
    {
        if (mode == SyncMode.Incremental && !string.IsNullOrWhiteSpace(stageId))
        {
            throw new InvalidOperationException("La sincronizacion incremental se ejecuta por pipeline, no por etapa.");
        }

        var connectionInfo = await repository.GetActiveConnectionAsync(cancellationToken);
        var pipeline = await GetPipelineAsync(pipelineSlug, cancellationToken);
        var entityType = !string.IsNullOrWhiteSpace(entityTypeSuffix)
            ? $"deal:{pipeline.Slug}:{entityTypeSuffix}"
            : string.IsNullOrWhiteSpace(stageId)
                ? $"deal:{pipeline.Slug}"
                : $"deal:{pipeline.Slug}:stage:{stageId}";
        var syncRunId = await repository.StartRunAsync(connectionInfo.Id, entityType, mode, cancellationToken);

        var recordsRead = 0;
        var recordsWritten = 0;
        DateTimeOffset? since = null;
        DateTimeOffset? cursor = null;
        var effectiveCreatedFrom = mode == SyncMode.Full ? createdFrom ?? FullSyncCreatedFrom : (DateTimeOffset?)null;
        var effectiveCreatedTo = mode == SyncMode.Full ? createdTo : null;

        try
        {
            since = mode == SyncMode.Incremental
                ? await GetIncrementalSinceAsync(connectionInfo.Id, pipeline.Id, entityType, cancellationToken)
                : null;
            cursor = since;

            await using var dbConnection = await dataSource.OpenConnectionAsync(cancellationToken);
            var firstPage = await FetchDealListPageAsync(pipeline.CategoryId, stageId, 0, since, effectiveCreatedFrom, effectiveCreatedTo, fieldEqualsFilters, selectFields, cancellationToken);
            cursor = await ProcessDealPageAsync(
                dbConnection,
                connectionInfo.Id,
                pipeline.Id,
                syncRunId,
                firstPage.Result,
                recordsRead,
                recordsWritten,
                cursor,
                cancellationToken,
                persistUnchangedPayload: mode == SyncMode.Full && reconcileMissing,
                progress => (recordsRead, recordsWritten, cursor) = progress);

            await repository.UpdateRunProgressAsync(syncRunId, recordsRead, recordsWritten, cancellationToken, cursor);

            if (firstPage.Total is not null)
            {
                // Requests with 50 deal-list commands and UF_* fields regularly exceed
                // Bitrix's 60-second gateway limit. Smaller batches keep the complete
                // custom-field payload while allowing long pipelines to finish.
                foreach (var chunk in GetDealListPageStarts(firstPage.Next, firstPage.Total.Value).Chunk(3))
                {
                    var pages = await FetchDealListPageBatchWithFallbackAsync(
                        pipeline.CategoryId,
                        stageId,
                        chunk,
                        since,
                        effectiveCreatedFrom,
                        effectiveCreatedTo,
                        fieldEqualsFilters,
                        selectFields,
                        cancellationToken);
                    foreach (var page in pages)
                    {
                        cursor = await ProcessDealPageAsync(
                            dbConnection,
                            connectionInfo.Id,
                            pipeline.Id,
                            syncRunId,
                            page,
                            recordsRead,
                            recordsWritten,
                            cursor,
                            cancellationToken,
                            persistUnchangedPayload: mode == SyncMode.Full && reconcileMissing,
                            progress => (recordsRead, recordsWritten, cursor) = progress);

                        await repository.UpdateRunProgressAsync(syncRunId, recordsRead, recordsWritten, cancellationToken, cursor);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                }
            }
            else
            {
                var start = firstPage.Next;
                var visitedStarts = new HashSet<int>();
                while (start is not null && visitedStarts.Add(start.Value))
                {
                    var page = await FetchDealListPageAsync(pipeline.CategoryId, stageId, start.Value, since, effectiveCreatedFrom, effectiveCreatedTo, fieldEqualsFilters, selectFields, cancellationToken);
                    if (page.Result.GetArrayLength() == 0)
                    {
                        break;
                    }

                    cursor = await ProcessDealPageAsync(
                        dbConnection,
                        connectionInfo.Id,
                        pipeline.Id,
                        syncRunId,
                        page.Result,
                        recordsRead,
                        recordsWritten,
                        cursor,
                        cancellationToken,
                        persistUnchangedPayload: mode == SyncMode.Full && reconcileMissing,
                        progress => (recordsRead, recordsWritten, cursor) = progress);

                    await repository.UpdateRunProgressAsync(syncRunId, recordsRead, recordsWritten, cancellationToken, cursor);
                    start = page.Next;
                }
            }

            if (mode == SyncMode.Full && reconcileMissing)
            {
                await ReconcileMissingDealsAsync(
                    dbConnection,
                    connectionInfo.Id,
                    pipeline.Id,
                    syncRunId,
                    stageId,
                    cancellationToken);
            }

            await repository.FinishRunAsync(syncRunId, "succeeded", recordsRead, recordsWritten, null, CancellationToken.None, cursor);
            return new SyncResult(syncRunId, entityType, ToDbMode(mode), "succeeded", recordsRead, recordsWritten);
        }
        catch (Exception ex)
        {
            await repository.FinishRunAsync(syncRunId, "failed", recordsRead, recordsWritten, ex.Message, CancellationToken.None);
            return new SyncResult(syncRunId, entityType, ToDbMode(mode), "failed", recordsRead, recordsWritten, ex.Message);
        }
    }

    private async Task<DealListPage> FetchDealListPageAsync(
        int categoryId,
        string? stageId,
        int start,
        DateTimeOffset? since,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        IReadOnlyDictionary<string, string>? fieldEqualsFilters,
        IReadOnlyList<string>? selectFields,
        CancellationToken cancellationToken)
    {
        using var response = await bitrixClient.CallAsync(
            BitrixMethod.DealList,
            BuildDealListParameters(categoryId, stageId, start, since, createdFrom, createdTo, fieldEqualsFilters, selectFields),
            cancellationToken);

        var root = response.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException(error.GetString());
        }

        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
        {
            return new DealListPage(JsonDocument.Parse("[]").RootElement.Clone(), null, null);
        }

        return new DealListPage(result.Clone(), TryGetNext(root), TryGetTotal(root));
    }

    private async Task<IReadOnlyList<JsonElement>> FetchDealListPageBatchAsync(
        int categoryId,
        string? stageId,
        int[] starts,
        DateTimeOffset? since,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        IReadOnlyDictionary<string, string>? fieldEqualsFilters,
        IReadOnlyList<string>? selectFields,
        CancellationToken cancellationToken)
    {
        if (starts.Length == 0)
        {
            return Array.Empty<JsonElement>();
        }

        var pages = new List<JsonElement>();
        var batchParameters = starts.Select(start =>
            new KeyValuePair<string, string>(
                $"cmd[deal_{start}]",
                BuildBatchCommand(BitrixMethod.DealList, BuildDealListParameters(categoryId, stageId, start, since, createdFrom, createdTo, fieldEqualsFilters, selectFields))));

        using var response = await bitrixClient.CallAsync(BitrixMethod.Batch, batchParameters, cancellationToken);
        var root = response.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException(error.GetString());
        }

        if (!root.TryGetProperty("result", out var batchResult))
        {
            return pages;
        }

        if (batchResult.TryGetProperty("result_error", out var resultErrors) &&
            resultErrors.ValueKind == JsonValueKind.Object &&
            resultErrors.EnumerateObject().Any())
        {
            var firstError = resultErrors.EnumerateObject().First();
            throw new InvalidOperationException($"Bitrix batch error in {firstError.Name}: {firstError.Value}");
        }

        if (batchResult.TryGetProperty("result", out var results) && results.ValueKind == JsonValueKind.Object)
        {
            foreach (var start in starts)
            {
                if (results.TryGetProperty($"deal_{start}", out var page) && page.ValueKind == JsonValueKind.Array)
                {
                    pages.Add(page.Clone());
                }
            }
        }

        return pages;
    }

    private async Task<IReadOnlyList<JsonElement>> FetchDealListPageBatchWithFallbackAsync(
        int categoryId,
        string? stageId,
        int[] starts,
        DateTimeOffset? since,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        IReadOnlyDictionary<string, string>? fieldEqualsFilters,
        IReadOnlyList<string>? selectFields,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FetchDealListPageBatchAsync(
                categoryId,
                stageId,
                starts,
                since,
                createdFrom,
                createdTo,
                fieldEqualsFilters,
                selectFields,
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (IsBitrixOperationTimeLimit(exception))
        {
            var pages = new List<JsonElement>();
            foreach (var start in starts)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                pages.Add((await FetchDealListPageWithRetryAsync(
                    categoryId,
                    stageId,
                    start,
                    since,
                    createdFrom,
                    createdTo,
                    fieldEqualsFilters,
                    selectFields,
                    cancellationToken)).Result);
            }

            return pages;
        }
    }

    private async Task<DealListPage> FetchDealListPageWithRetryAsync(
        int categoryId,
        string? stageId,
        int start,
        DateTimeOffset? since,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        IReadOnlyDictionary<string, string>? fieldEqualsFilters,
        IReadOnlyList<string>? selectFields,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await FetchDealListPageAsync(
                    categoryId,
                    stageId,
                    start,
                    since,
                    createdFrom,
                    createdTo,
                    fieldEqualsFilters,
                    selectFields,
                    cancellationToken);
            }
            catch (InvalidOperationException exception) when (IsBitrixOperationTimeLimit(exception) && attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * (attempt + 1)), cancellationToken);
            }
        }
    }

    private static bool IsBitrixOperationTimeLimit(Exception exception) =>
        exception.Message.Contains("OPERATION_TIME_LIMIT", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<int> GetDealListPageStarts(int? firstStart, int total)
    {
        const int bitrixPageSize = 50;
        if (firstStart is null || firstStart.Value >= total)
        {
            yield break;
        }

        for (var start = firstStart.Value; start < total; start += bitrixPageSize)
        {
            yield return start;
        }
    }

    private async Task<DateTimeOffset?> ProcessDealPageAsync(
        NpgsqlConnection dbConnection,
        Guid connectionId,
        Guid pipelineId,
        Guid syncRunId,
        JsonElement result,
        int recordsRead,
        int recordsWritten,
        DateTimeOffset? cursor,
        CancellationToken cancellationToken,
        bool persistUnchangedPayload,
        Action<(int RecordsRead, int RecordsWritten, DateTimeOffset? Cursor)> setProgress)
    {
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            setProgress((recordsRead, recordsWritten, cursor));
            return cursor;
        }

        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);
        var currentHashes = await LoadCurrentDealHashesAsync(
            dbConnection,
            transaction,
            connectionId,
            result.EnumerateArray()
                .Select(deal => GetString(deal, "ID"))
                .Where(bitrixId => !string.IsNullOrWhiteSpace(bitrixId))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            cancellationToken);

        foreach (var deal in result.EnumerateArray())
        {
            recordsRead++;
            var bitrixId = GetString(deal, "ID")
                ?? throw new InvalidOperationException("Bitrix deal without ID.");
            var modifiedAt = GetDateTimeOffset(deal, "DATE_MODIFY");
            if (modifiedAt is not null && (cursor is null || modifiedAt > cursor))
            {
                cursor = modifiedAt;
            }

            if (await UpsertDealAsync(
                dbConnection,
                transaction,
                connectionId,
                pipelineId,
                syncRunId,
                deal,
                bitrixId,
                currentHashes.GetValueOrDefault(bitrixId),
                cancellationToken,
                persistUnchangedPayload))
            {
                recordsWritten++;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        setProgress((recordsRead, recordsWritten, cursor));
        return cursor;
    }

    private static string BuildBatchCommand(string method, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var query = string.Join(
            "&",
            parameters.Select(parameter =>
                $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

        return $"{method}?{query}";
    }

    private static async Task ReconcileMissingDealsAsync(
        NpgsqlConnection connection,
        Guid connectionId,
        Guid pipelineId,
        Guid syncRunId,
        string? stageId,
        CancellationToken cancellationToken)
    {
        var stageFilter = string.IsNullOrWhiteSpace(stageId)
            ? string.Empty
            : "AND snapshot.stage_id = @stageId";

        var sql = $"""
            UPDATE bitrix.entity_snapshots snapshot
            SET is_deleted = true,
                deleted_detected_at = now()
            WHERE snapshot.connection_id = @connectionId
              AND snapshot.pipeline_id = @pipelineId
              AND snapshot.entity_type = 'deal'
              AND snapshot.is_deleted = false
              {stageFilter}
              AND NOT EXISTS (
                  SELECT 1
                  FROM bitrix.raw_payloads payload
                  WHERE payload.sync_run_id = @syncRunId
                    AND payload.entity_type = 'deal'
                    AND payload.bitrix_id = snapshot.bitrix_id
              );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("pipelineId", pipelineId);
        command.Parameters.AddWithValue("syncRunId", syncRunId);
        if (!string.IsNullOrWhiteSpace(stageId))
        {
            command.Parameters.AddWithValue("stageId", stageId);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildDealListParameters(
        int categoryId,
        string? stageId,
        int start,
        DateTimeOffset? since,
        DateTimeOffset? createdFrom,
        DateTimeOffset? createdTo,
        IReadOnlyDictionary<string, string>? fieldEqualsFilters,
        IReadOnlyList<string>? selectFields)
    {
        yield return new KeyValuePair<string, string>("filter[CATEGORY_ID]", categoryId.ToString());
        if (!string.IsNullOrWhiteSpace(stageId))
        {
            yield return new KeyValuePair<string, string>("filter[STAGE_ID]", stageId);
        }

        if (since is not null)
        {
            // A small overlap prevents missing records modified at the cursor boundary.
            yield return new KeyValuePair<string, string>(
                "filter[>=DATE_MODIFY]",
                since.Value.Subtract(TimeSpan.FromMinutes(2)).ToString("O", CultureInfo.InvariantCulture));
        }

        if (createdFrom is not null)
        {
            yield return new KeyValuePair<string, string>(
                "filter[>=DATE_CREATE]",
                createdFrom.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        if (createdTo is not null)
        {
            yield return new KeyValuePair<string, string>(
                "filter[<=DATE_CREATE]",
                createdTo.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        if (fieldEqualsFilters is not null)
        {
            foreach (var filter in fieldEqualsFilters)
            {
                yield return new KeyValuePair<string, string>($"filter[{filter.Key}]", filter.Value);
            }
        }

        yield return new KeyValuePair<string, string>("order[DATE_MODIFY]", "ASC");
        yield return new KeyValuePair<string, string>("order[ID]", "ASC");
        yield return new KeyValuePair<string, string>("start", start.ToString());
        foreach (var field in selectFields ?? DealSummaryFields)
        {
            yield return new KeyValuePair<string, string>("select[]", field);
        }
    }

    private async Task<DateTimeOffset?> GetIncrementalSinceAsync(
        Guid connectionId,
        Guid pipelineId,
        string entityType,
        CancellationToken cancellationToken)
    {
        var savedCursor = await repository.GetLastSuccessfulCursorAsync(connectionId, entityType, cancellationToken);
        if (savedCursor is not null)
        {
            return savedCursor;
        }

        const string sql = """
            SELECT max(bitrix_updated_at)
            FROM bitrix.deals
            WHERE connection_id = @connectionId
              AND pipeline_id = @pipelineId
              AND EXISTS (
                  SELECT 1
                  FROM bitrix.sync_runs
                  WHERE connection_id = @connectionId
                    AND entity_type = @entityType
                    AND mode = 'full'
                    AND status = 'succeeded'
                    AND records_read > 0
              );
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("pipelineId", pipelineId);
        command.Parameters.AddWithValue("entityType", entityType);
        var value = await command.ExecuteScalarAsync(cancellationToken);

        if (value is DateTimeOffset cursor)
        {
            return cursor.ToUniversalTime();
        }
        if (value is DateTime dateTime)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
        }

        throw new InvalidOperationException(
            $"La pipeline '{entityType}' no tiene una carga completa valida. Ejecute primero la carga inicial de esa pipeline.");
    }

    private async Task<PipelineRecord> GetPipelineAsync(string slug, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, slug, name, category_id, domain
            FROM bitrix.pipelines
            WHERE slug = @slug AND is_active = true
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("slug", slug);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Pipeline '{slug}' does not exist or is inactive.");
        }

        return new PipelineRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4));
    }

    private async Task<PipelineRecord?> GetPipelineByCategoryIdAsync(string categoryId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(categoryId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCategoryId))
        {
            throw new InvalidOperationException($"Bitrix category '{categoryId}' is not valid.");
        }

        const string sql = """
            SELECT id, slug, name, category_id, domain
            FROM bitrix.pipelines
            WHERE category_id = @categoryId AND is_active = true
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("categoryId", parsedCategoryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new PipelineRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4));
    }

    private bool PipelineDomainIsAllowed(string domain)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookAllowedPipelineDomains))
        {
            return true;
        }

        return _options.WebhookAllowedPipelineDomains
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(allowedDomain => string.Equals(allowedDomain, domain, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> UpsertDealAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        Guid pipelineId,
        Guid syncRunId,
        JsonElement deal,
        string bitrixId,
        string? currentHash,
        CancellationToken cancellationToken,
        bool persistUnchangedPayload = false)
    {
        var payload = SanitizeJsonForPostgres(JsonSerializer.Serialize(deal));
        var hash = Sha256(payload);
        var customFields = ExtractCustomFields(deal);
        var coreData = ExtractCoreData(deal);

        if (string.Equals(currentHash, hash, StringComparison.OrdinalIgnoreCase) && !persistUnchangedPayload)
        {
            return false;
        }

        var rawPayloadId = await InsertRawPayloadAsync(
            connection,
            transaction,
            connectionId,
            pipelineId,
            syncRunId,
            bitrixId,
            payload,
            hash,
            cancellationToken);

        await UpsertDealRowAsync(
            connection,
            transaction,
            connectionId,
            pipelineId,
            rawPayloadId,
            deal,
            bitrixId,
            cancellationToken);

        await UpsertEntitySnapshotAsync(
            connection,
            transaction,
            connectionId,
            pipelineId,
            rawPayloadId,
            deal,
            bitrixId,
            coreData,
            customFields,
            cancellationToken);

        return true;
    }

    private static async Task<IReadOnlyDictionary<string, string>> LoadCurrentDealHashesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        string[] bitrixIds,
        CancellationToken cancellationToken)
    {
        if (bitrixIds.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        const string sql = """
            SELECT es.bitrix_id, rp.payload_hash
            FROM bitrix.entity_snapshots es
            JOIN bitrix.raw_payloads rp ON rp.id = es.raw_payload_id
            WHERE es.connection_id = @connectionId
              AND es.entity_type = 'deal'
              AND es.bitrix_id = ANY(@bitrixIds)
              AND es.is_deleted = false;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("bitrixIds", bitrixIds);

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hashes[reader.GetString(0)] = reader.GetString(1);
        }

        return hashes;
    }

    private static async Task<Guid> InsertRawPayloadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        Guid pipelineId,
        Guid syncRunId,
        string bitrixId,
        string payload,
        string hash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO bitrix.raw_payloads (
                connection_id,
                pipeline_id,
                sync_run_id,
                entity_type,
                bitrix_id,
                payload,
                payload_hash
            )
            VALUES (
                @connectionId,
                @pipelineId,
                @syncRunId,
                'deal',
                @bitrixId,
                @payload,
                @payloadHash
            )
            ON CONFLICT (connection_id, entity_type, bitrix_id, payload_hash) DO UPDATE
            SET pipeline_id = EXCLUDED.pipeline_id,
                sync_run_id = EXCLUDED.sync_run_id,
                received_at = bitrix.raw_payloads.received_at
            RETURNING id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("pipelineId", pipelineId);
        command.Parameters.AddWithValue("syncRunId", syncRunId);
        command.Parameters.AddWithValue("bitrixId", bitrixId);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = payload;
        command.Parameters.AddWithValue("payloadHash", hash);

        return (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Could not insert raw deal payload."));
    }

    private static async Task UpsertDealRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        Guid pipelineId,
        Guid rawPayloadId,
        JsonElement deal,
        string bitrixId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO bitrix.deals (
                connection_id,
                pipeline_id,
                bitrix_id,
                title,
                stage_id,
                category_id,
                assigned_by_bitrix_id,
                opportunity,
                currency_id,
                is_closed,
                raw_payload_id,
                bitrix_created_at,
                bitrix_updated_at,
                closed_at
            )
            VALUES (
                @connectionId,
                @pipelineId,
                @bitrixId,
                @title,
                @stageId,
                @categoryId,
                @assignedByBitrixId,
                @opportunity,
                @currencyId,
                @isClosed,
                @rawPayloadId,
                @bitrixCreatedAt,
                @bitrixUpdatedAt,
                @closedAt
            )
            ON CONFLICT (connection_id, bitrix_id) DO UPDATE
            SET pipeline_id = EXCLUDED.pipeline_id,
                title = EXCLUDED.title,
                stage_id = EXCLUDED.stage_id,
                category_id = EXCLUDED.category_id,
                assigned_by_bitrix_id = EXCLUDED.assigned_by_bitrix_id,
                opportunity = EXCLUDED.opportunity,
                currency_id = EXCLUDED.currency_id,
                is_closed = EXCLUDED.is_closed,
                raw_payload_id = EXCLUDED.raw_payload_id,
                bitrix_created_at = EXCLUDED.bitrix_created_at,
                bitrix_updated_at = EXCLUDED.bitrix_updated_at,
                closed_at = EXCLUDED.closed_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("pipelineId", pipelineId);
        command.Parameters.AddWithValue("bitrixId", bitrixId);
        command.Parameters.AddWithValue("title", GetString(deal, "TITLE") ?? $"Deal {bitrixId}");
        command.Parameters.AddWithValue("stageId", (object?)GetString(deal, "STAGE_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("categoryId", (object?)GetString(deal, "CATEGORY_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("assignedByBitrixId", (object?)GetString(deal, "ASSIGNED_BY_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("opportunity", (object?)GetDecimal(deal, "OPPORTUNITY") ?? DBNull.Value);
        command.Parameters.AddWithValue("currencyId", (object?)GetString(deal, "CURRENCY_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("isClosed", GetString(deal, "CLOSED") == "Y");
        command.Parameters.AddWithValue("rawPayloadId", rawPayloadId);
        command.Parameters.AddWithValue("bitrixCreatedAt", (object?)GetDateTimeOffset(deal, "DATE_CREATE") ?? DBNull.Value);
        command.Parameters.AddWithValue("bitrixUpdatedAt", (object?)GetDateTimeOffset(deal, "DATE_MODIFY") ?? DBNull.Value);
        command.Parameters.AddWithValue("closedAt", (object?)GetDateTimeOffset(deal, "CLOSEDATE") ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertEntitySnapshotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid connectionId,
        Guid pipelineId,
        Guid rawPayloadId,
        JsonElement deal,
        string bitrixId,
        string coreData,
        string customFields,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO bitrix.entity_snapshots (
                connection_id,
                pipeline_id,
                entity_type,
                bitrix_id,
                title,
                assigned_by_bitrix_id,
                stage_id,
                category_id,
                bitrix_created_at,
                bitrix_updated_at,
                core_data,
                custom_fields,
                raw_payload_id
            )
            VALUES (
                @connectionId,
                @pipelineId,
                'deal',
                @bitrixId,
                @title,
                @assignedByBitrixId,
                @stageId,
                @categoryId,
                @bitrixCreatedAt,
                @bitrixUpdatedAt,
                @coreData,
                @customFields,
                @rawPayloadId
            )
            ON CONFLICT (connection_id, entity_type, bitrix_id) DO UPDATE
            SET pipeline_id = EXCLUDED.pipeline_id,
                title = EXCLUDED.title,
                assigned_by_bitrix_id = EXCLUDED.assigned_by_bitrix_id,
                stage_id = EXCLUDED.stage_id,
                category_id = EXCLUDED.category_id,
                bitrix_created_at = EXCLUDED.bitrix_created_at,
                bitrix_updated_at = EXCLUDED.bitrix_updated_at,
                core_data = EXCLUDED.core_data,
                -- Pipeline-specific Bitrix responses can omit custom fields that
                -- were returned by an earlier/full synchronization.  Merge the
                -- payloads so a partial response updates the fields it contains
                -- without erasing report-critical values such as radication year.
                custom_fields = COALESCE(bitrix.entity_snapshots.custom_fields, '{}'::jsonb)
                    || COALESCE(EXCLUDED.custom_fields, '{}'::jsonb),
                raw_payload_id = EXCLUDED.raw_payload_id,
                is_deleted = false,
                deleted_detected_at = null;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("connectionId", connectionId);
        command.Parameters.AddWithValue("pipelineId", pipelineId);
        command.Parameters.AddWithValue("bitrixId", bitrixId);
        command.Parameters.AddWithValue("title", (object?)GetString(deal, "TITLE") ?? DBNull.Value);
        command.Parameters.AddWithValue("assignedByBitrixId", (object?)GetString(deal, "ASSIGNED_BY_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("stageId", (object?)GetString(deal, "STAGE_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("categoryId", (object?)GetString(deal, "CATEGORY_ID") ?? DBNull.Value);
        command.Parameters.AddWithValue("bitrixCreatedAt", (object?)GetDateTimeOffset(deal, "DATE_CREATE") ?? DBNull.Value);
        command.Parameters.AddWithValue("bitrixUpdatedAt", (object?)GetDateTimeOffset(deal, "DATE_MODIFY") ?? DBNull.Value);
        command.Parameters.Add("coreData", NpgsqlDbType.Jsonb).Value = coreData;
        command.Parameters.Add("customFields", NpgsqlDbType.Jsonb).Value = customFields;
        command.Parameters.AddWithValue("rawPayloadId", rawPayloadId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ExtractCustomFields(JsonElement deal)
    {
        var custom = deal.EnumerateObject()
            .Where(property => property.Name.StartsWith("UF_CRM", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(property => property.Name, property => property.Value.Clone());

        return SanitizeJsonForPostgres(JsonSerializer.Serialize(custom));
    }

    private static string ExtractCoreData(JsonElement deal)
    {
        var core = deal.EnumerateObject()
            .Where(property => !property.Name.StartsWith("UF_CRM", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(property => property.Name, property => property.Value.Clone());

        return SanitizeJsonForPostgres(JsonSerializer.Serialize(core));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString().Replace("\0", string.Empty, StringComparison.Ordinal)
            : null;
    }

    private static string SanitizeJsonForPostgres(string json) =>
        json.Replace("\\u0000", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\0", string.Empty, StringComparison.Ordinal);

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static int? TryGetNext(JsonElement root)
    {
        if (!root.TryGetProperty("next", out var next))
        {
            return null;
        }

        if (next.ValueKind == JsonValueKind.Number && next.TryGetInt32(out var nextNumber))
        {
            return nextNumber;
        }

        if (next.ValueKind == JsonValueKind.String && int.TryParse(next.GetString(), out var nextStringNumber))
        {
            return nextStringNumber;
        }

        return null;
    }

    private static int? TryGetTotal(JsonElement root)
    {
        if (!root.TryGetProperty("total", out var total))
        {
            return null;
        }

        if (total.ValueKind == JsonValueKind.Number && total.TryGetInt32(out var totalNumber))
        {
            return totalNumber;
        }

        if (total.ValueKind == JsonValueKind.String && int.TryParse(total.GetString(), out var totalStringNumber))
        {
            return totalStringNumber;
        }

        return null;
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ToDbMode(SyncMode mode) => mode switch
    {
        SyncMode.Full => "full",
        SyncMode.Incremental => "incremental",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private sealed record PipelineRecord(Guid Id, string Slug, string Name, int CategoryId, string Domain);
    private sealed record DealListPage(JsonElement Result, int? Next, int? Total);
}
