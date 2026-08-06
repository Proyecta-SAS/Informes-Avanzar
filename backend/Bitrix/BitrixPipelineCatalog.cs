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
        new("informes_bi_builder", "Informes BI Builder", 224, "comercial", 44),
        new("ins_libranza", "INS Libranza", 107, "operaciones", 50),
        new("ins_embargos", "INS Embargos", 109, "operaciones", 52),
        new("pqrfs", "PQRFS", 97, "servicio_cliente", 55),
        new("seguros_operativa", "Seguros Operativa", 256, "seguros", 60),
        new("seguros_comercial", "Seguros Comercial", 278, "seguros", 62)
    ];

    public IReadOnlyList<BitrixPipeline> ListDefaults() => Defaults;
}
