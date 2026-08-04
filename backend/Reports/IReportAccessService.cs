namespace InformesAvanzar.Api.Reports;

public interface IReportAccessService
{
    Task<bool> UserCanAccessReportAsync(
        Guid userId,
        Guid reportDefinitionId,
        CancellationToken cancellationToken);
    Task<bool> UserCanAccessReportAsync(Guid userId, string reportCode, CancellationToken cancellationToken);
    Task<string[]> GetAccessibleReportCodesAsync(Guid userId, CancellationToken cancellationToken);
}
