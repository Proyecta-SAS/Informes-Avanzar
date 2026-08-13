BEGIN;

DO $$
DECLARE
    category_pipeline_id uuid;
    canonical_slug_pipeline_id uuid;
BEGIN
    SELECT id
    INTO category_pipeline_id
    FROM bitrix.pipelines
    WHERE category_id = 72;

    SELECT id
    INTO canonical_slug_pipeline_id
    FROM bitrix.pipelines
    WHERE slug = 'cuentas_cobro';

    IF category_pipeline_id IS NULL AND canonical_slug_pipeline_id IS NULL THEN
        INSERT INTO bitrix.pipelines (
            slug,
            name,
            category_id,
            domain,
            sync_order,
            is_active
        )
        VALUES (
            'cuentas_cobro',
            'Cuentas de Cobro',
            72,
            'comercial',
            70,
            true
        );
    ELSIF category_pipeline_id IS NULL THEN
        UPDATE bitrix.pipelines
        SET category_id = 72,
            name = 'Cuentas de Cobro',
            domain = 'comercial',
            sync_order = 70,
            is_active = true
        WHERE id = canonical_slug_pipeline_id;
    ELSIF canonical_slug_pipeline_id IS NULL
        OR canonical_slug_pipeline_id = category_pipeline_id THEN
        UPDATE bitrix.pipelines
        SET slug = 'cuentas_cobro',
            name = 'Cuentas de Cobro',
            domain = 'comercial',
            sync_order = 70,
            is_active = true
        WHERE id = category_pipeline_id;
    ELSE
        RAISE EXCEPTION
            'Cannot normalize category 72: slug cuentas_cobro belongs to another pipeline.';
    END IF;
END
$$;

UPDATE bitrix.pipelines pipeline
SET area_id = area.id
FROM bitrix.areas area
WHERE pipeline.category_id = 72
  AND area.slug = 'comercial';

COMMIT;
