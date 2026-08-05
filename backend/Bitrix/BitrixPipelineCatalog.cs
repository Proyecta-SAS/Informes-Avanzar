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
        new("pnnc_operativa", "PNNC Operativa", 28, "operaciones", 40),
        new("1116_comercial", "1116 Comercial", 30, "comercial", 41),
        new("1116_operativa", "1116 Operativa", 32, "operaciones", 42),
        new("lp_2445_operativa", "LP-2445 Operativa", 248, "operaciones", 43),
        new("informes_bi_builder", "Informes BI Builder", 224, "comercial", 44)
    ];

    public IReadOnlyList<BitrixPipeline> ListDefaults() => Defaults;
}
