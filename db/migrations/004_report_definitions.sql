BEGIN;

INSERT INTO reporting.report_definitions (code, name, description, query_key, status)
VALUES
    ('rch_comercial', 'RCH Comercial', 'Negociaciones comerciales RCH.', 'rch_comercial', 'published'),
    ('rch_operativa', 'RCH Operativa', 'Seguimiento operativo RCH.', 'rch_operativa', 'published'),
    ('pnnc_comercial', 'PNNC Comercial', 'Negociaciones comerciales PNNC.', 'pnnc_comercial', 'published'),
    ('pnnc_operativa', 'PNNC Operativa', 'Seguimiento operativo PNNC.', 'pnnc_operativa', 'published'),
    ('fuerza_comercial_diego', 'Fuerza Comercial Diego', 'Panel comercial consolidado de Diego.', 'fuerza_comercial_diego', 'published')
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    description = EXCLUDED.description,
    query_key = EXCLUDED.query_key,
    status = EXCLUDED.status;

COMMIT;
