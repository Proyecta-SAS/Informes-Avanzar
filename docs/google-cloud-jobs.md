# Sincronizaciones programadas en Google Cloud

Las sincronizaciones programadas deben ejecutarse como Cloud Run Jobs, no como peticiones largas al servicio web.

## Jobs

Los dos jobs usan la misma imagen del servicio `informes-avanzar`, la misma cuenta de servicio, la conexión de Cloud SQL y los mismos secretos.

### Incremental

- Nombre sugerido: `informes-bitrix-incremental`.
- Variable adicional: `BITRIX_SYNC_MODE=incremental`.
- Timeout: 2 horas.
- Tareas: 1.
- Reintentos: 0.
- Cron: `0 0-1,7-23 * * *`.
- Zona horaria: `America/Bogota`.

Se ejecuta cada hora desde las 07:00 hasta las 23:00 y también a las 00:00 y 01:00.

### Reconciliación global

- Nombre sugerido: `informes-bitrix-global`.
- Variable adicional: `BITRIX_SYNC_MODE=full`.
- Timeout: 18 horas.
- Tareas: 1.
- Reintentos: 0.
- Cron: `15 3 * * *`.
- Zona horaria: `America/Bogota`.

Se ejecuta diariamente a las 03:15, dejando más de dos horas desde la incremental de la 01:00.

## Variables y secretos

Cada job requiere:

- `ConnectionStrings__Default`, desde `informes-db-connection:latest`.
- `BITRIX_WEBHOOK_URL`, desde `bitrix-webhook-url:latest`.
- `ADMIN_API_KEY`, desde `informes-admin-api-key:latest`.
- `ASPNETCORE_ENVIRONMENT=Production`.
- `BITRIX_SYNC_MODE`, con el valor correspondiente al job.

Ambos deben incluir la conexión:

```text
db-mensajeria:us-central1:informes-avanzar-db
```

## Concurrencia y recuperación

- `bitrix.sync_locks` impide ejecuciones globales simultáneas.
- La incremental conserva un bloqueo por un máximo de 2 horas.
- La global conserva un bloqueo por un máximo de 20 horas y su tarea tiene timeout de 18 horas.
- Una ejecución interrumpida no elimina páginas ya confirmadas.
- Los payloads idénticos no se vuelven a escribir.
- Los runs abandonados se recuperan después de 2 horas.

La falta de una ejecución incremental mientras la global está activa no pierde datos: la siguiente incremental continúa desde el último cursor exitoso.

## Validación

Antes de activar cada horario, ejecutar el job manualmente y confirmar:

1. La ejecución termina correctamente.
2. Los logs muestran resultados por entidad.
3. `bitrix.sync_runs` registra el modo esperado.
4. No queda un run en estado `running`.
5. Los informes siguen respondiendo desde PostgreSQL.
