namespace InformesAvanzar.Api.Bitrix;

public sealed record BitrixPipeline(
    string Slug,
    string Name,
    int CategoryId,
    string Domain,
    int SyncOrder);
