namespace InformesAvanzar.Api.Data;

public sealed record CommercialReportScope(
    bool CanViewAll,
    string CommercialRole,
    string[] AllowedBitrixUserIds,
    bool BlocksConfigured,
    string[] VisibleBlocks)
{
    public bool CanViewBlock(string blockCode) =>
        !BlocksConfigured || VisibleBlocks.Contains(blockCode, StringComparer.OrdinalIgnoreCase);

    public bool CanViewAnyBlock(params string[] blockCodes) =>
        !BlocksConfigured || blockCodes.Any(CanViewBlock);
}
