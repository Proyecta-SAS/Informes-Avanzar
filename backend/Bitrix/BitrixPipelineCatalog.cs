namespace InformesAvanzar.Api.Bitrix;

public interface IBitrixPipelineCatalog
{
    IReadOnlyList<BitrixPipeline> ListDefaults();
}

public sealed class BitrixPipelineCatalog : IBitrixPipelineCatalog
{
    private static readonly BitrixPipeline[] Defaults =
    [
        new("rch_comercial", "RCH Comercial", 8, "comercial", 10),
        new("rch_operativa", "RCH Operativa", 10, "operaciones", 20),
        new("pnnc_comercial", "PNNC Comercial", 26, "comercial", 30),
        new("pnnc_operativa", "PNNC Operativa", 28, "operaciones", 40)
    ];

    public IReadOnlyList<BitrixPipeline> ListDefaults() => Defaults;
}
