BEGIN;

INSERT INTO bitrix.pipelines (slug, name, category_id, domain, sync_order)
VALUES
    ('1116_operativa', '1116 Operativa', 32, 'operaciones', 42),
    ('lp_2445_operativa', 'LP-2445 Operativa', 248, 'operaciones', 43)
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
