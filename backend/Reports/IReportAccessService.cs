namespace InformesAvanzar.Api.Reports;

public interface IReportAccessService
{
    Task<bool> UserCanAccessReportAsync(
        Guid userId,
        Guid reportDefinitionId,
        CancellationToken cancellationToken);
}
