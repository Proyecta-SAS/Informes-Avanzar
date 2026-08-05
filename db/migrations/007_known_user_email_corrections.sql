BEGIN;

UPDATE bitrix.users
SET email = 'ma.galeano@avanzarsoluciones.com',
    updated_at = now()
WHERE bitrix_id = '18434'
  AND email IS DISTINCT FROM 'ma.galeano@avanzarsoluciones.com';

COMMIT;
