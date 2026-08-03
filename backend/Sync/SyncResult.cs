namespace InformesAvanzar.Api.Sync;

public sealed record SyncResult(
    Guid SyncRunId,
    string EntityType,
    string Mode,
    string Status,
    int RecordsRead,
    int RecordsWritten,
    string? ErrorMessage = null);
