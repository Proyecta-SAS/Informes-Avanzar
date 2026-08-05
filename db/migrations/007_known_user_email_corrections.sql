BEGIN;

UPDATE bitrix.users
SET email = 'ma.galeano@avanzarsoluciones.com',
    updated_at = now()
WHERE bitrix_id = '18434'
  AND email IS DISTINCT FROM 'ma.galeano@avanzarsoluciones.com';

UPDATE bitrix.departments
SET head_bitrix_id = '18434',
    updated_at = now()
WHERE id = 1324
  AND head_bitrix_id IS DISTINCT FROM '18434';

COMMIT;
