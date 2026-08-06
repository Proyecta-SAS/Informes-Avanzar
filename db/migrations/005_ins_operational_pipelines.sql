BEGIN;

INSERT INTO bitrix.pipelines (slug, name, category_id, domain, sync_order)
VALUES
    ('ins_libranza', 'INS Libranza', 107, 'operaciones', 50),
    ('ins_embargos', 'INS Embargos', 109, 'operaciones', 52),
    ('pqrfs', 'PQRFS', 97, 'servicio_cliente', 55),
    ('seguros_operativa', 'Seguros Operativa', 256, 'seguros', 60),
    ('seguros_comercial', 'Seguros Comercial', 278, 'seguros', 62)
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
