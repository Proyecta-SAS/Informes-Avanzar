BEGIN;

INSERT INTO bitrix.pipelines (slug, name, category_id, domain, sync_order)
VALUES
    ('rch_cartera', 'RCH Cartera', 12, 'cartera', 50),
    ('pnnc_cartera', 'PNNC Cartera', 68, 'cartera', 60),
    ('cuentas_cobro', 'Cuentas de Cobro', 72, 'comercial', 70),
    ('informes_bi_builder', 'Informes BI Builder', 224, 'comercial', 80),
    ('cobro_juridico_rch', 'Cobro Juridico RCH', 302, 'cartera', 90),
    ('cobro_juridico_pnnc', 'Cobro Juridico PNNC', 308, 'cartera', 100)
ON CONFLICT (slug) DO UPDATE
SET name = EXCLUDED.name,
    category_id = EXCLUDED.category_id,
    domain = EXCLUDED.domain,
    sync_order = EXCLUDED.sync_order,
    is_active = true;

UPDATE bitrix.pipelines p
SET area_id = a.id
FROM bitrix.areas a
WHERE p.domain = a.slug
  AND p.area_id IS NULL;

COMMIT;
