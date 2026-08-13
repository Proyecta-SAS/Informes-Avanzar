namespace InformesAvanzar.Api.Sync;

public sealed class SyncAlreadyRunningException(string message) : InvalidOperationException(message);
