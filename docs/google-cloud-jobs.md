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

### Comercial nocturna de produccion

- Job: `informes-bitrix-commercial-nightly`.
- Scheduler: `scheduler-informes-bitrix-commercial-nightly`.
- Variable adicional: `BITRIX_SYNC_MODE=commercial-nightly`.
- Ano objetivo: `BITRIX_COMMERCIAL_SYNC_YEAR=2026`.
- Timeout: 8 horas.
- Tareas: 1.
- Reintentos del job: 0.
- Cron: `0 3 * * *`.
- Zona horaria: `America/Bogota`.

Se ejecuta diariamente a las 03:00. Este job no sincroniza usuarios, etapas, actividades ni todas las pipelines activas. Solo ejecuta:

- `rch_comercial`
- `pnnc_comercial`
- `rch_operativa`
- `pnnc_operativa`

En comerciales 2026 no borra registros locales que Bitrix no devuelva en la corrida (`reconcileMissing=false`). Esta proteccion evita borrados erroneos cuando Bitrix corta una consulta grande o devuelve error a mitad de paginacion. Si aparecen registros extra en produccion, primero se deben comparar IDs y validar una muestra en Bitrix antes de eliminar o reconciliar.

### Comercial extendida nocturna

- Job sugerido: `informes-bitrix-commercial-extended-nightly`.
- Scheduler sugerido: `scheduler-informes-bitrix-commercial-extended-nightly`.
- Variable adicional: `BITRIX_SYNC_MODE=commercial-extended-nightly`.
- Ano objetivo: `BITRIX_COMMERCIAL_SYNC_YEAR=2026`.
- Timeout sugerido: 12 horas.
- Tareas: 1.
- Reintentos del job: 0.
- Cron sugerido: `30 5 * * *`.
- Zona horaria: `America/Bogota`.

Se ejecuta separada de la comercial nocturna para refrescar pipelines de soporte sin aumentar el riesgo de timeout del job principal. Todas estas sincronizaciones se filtran al ano objetivo por fecha de creacion y usan `reconcileMissing=false`: actualizan e insertan registros, pero no marcan como borrados los registros locales que Bitrix no devuelva en una corrida incompleta.

Sincroniza:

- `cuentas_cobro`.
- `1116_comercial`.
- `rch_cartera`.
- `pnnc_cartera`.
- `1116_operativa`.
- `lp_2445_operativa`.
- `cobro_juridico_rch`.
- `cobro_juridico_pnnc`.

Comando manual:

```powershell
gcloud.cmd run jobs execute informes-bitrix-commercial-nightly --project=db-mensajeria --region=europe-west1
```

Validacion rapida:

```powershell
gcloud.cmd run jobs executions list --job=informes-bitrix-commercial-nightly --project=db-mensajeria --region=europe-west1 --limit=5
```

```powershell
gcloud.cmd logging read 'resource.type="cloud_run_job" AND resource.labels.job_name="informes-bitrix-commercial-nightly"' --project=db-mensajeria --freshness=18h --limit=160 --format='value(timestamp,severity,textPayload)'
```

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
