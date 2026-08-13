# Informes Avanzar

Aplicación para traer información de Bitrix24, conservarla en PostgreSQL y construir informes internos sin consultar Bitrix cada vez que un usuario abre una pantalla.

Este README está pensado como guía de incorporación para las personas que continúen desarrollando u operando el proyecto.

## Conceptos principales

### Sincronización

La **sincronización** copia datos desde Bitrix hacia PostgreSQL.

```text
Bitrix24 -> API REST de Bitrix -> servicios de sincronización -> PostgreSQL
```

En el flujo de negocios, cada registro se conserva en tres capas:

- `bitrix.raw_payloads`: respuesta original recibida desde Bitrix, identificada por hash.
- `bitrix.deals`: columnas normalizadas para filtros, relaciones y agregaciones.
- `bitrix.entity_snapshots`: snapshot operativo con datos base y campos personalizados en JSONB.

El estado de cada ejecución se registra en `bitrix.sync_runs`, incluyendo modo, estado, registros leídos, registros escritos, errores y cursor incremental.

La sincronización no genera directamente una pantalla ni un archivo de informe: alimenta la base de datos que consumen los informes.

### Actualización de informes

La **actualización de un informe** consiste en volver a consultar PostgreSQL y renderizar los resultados actuales.

```text
PostgreSQL -> endpoint de reporte -> JavaScript -> interfaz del informe
```

Actualmente los informes se actualizan al abrir o recargar la vista. No existe todavía un job independiente que materialice o recalcule todos los informes. Las tablas `reporting.report_runs` y `reporting.report_snapshots` están preparadas para implementar ese proceso más adelante.

Por tanto:

- Si Bitrix cambió y el cambio aún no está en PostgreSQL, se debe ejecutar una **sincronización**.
- Si PostgreSQL ya contiene el cambio, basta con **recargar o actualizar el informe**.
- Los informes nunca deben consultar Bitrix directamente desde el navegador.

## Estado actual

Está implementado:

- Cliente REST de Bitrix mediante webhook entrante.
- Sincronización paginada de usuarios.
- Sincronización de departamentos de usuarios.
- Sincronización de etapas de negocios.
- Sincronización completa de negocios por pipeline o por etapa.
- Sincronización incremental de negocios por pipeline.
- Sincronización global incremental de todas las pipelines activas.
- Sincronización masiva de resúmenes de negocios.
- Persistencia de payloads, datos normalizados, snapshots, historial y cursores.
- Bloqueo global para evitar dos sincronizaciones globales simultáneas.
- Interfaz de operación en `/sincronizacion.html`.
- Consultas generales de negocios, etapas, responsables y estado.
- Dashboard especial `Fuerza Comercial Diego`.
- Interfaz inicial de administración de usuarios, roles y acceso a informes.
- Estructura de usuarios, roles, permisos y definiciones publicadas de informes.

Está preparado en base de datos, pero aún no tiene sincronizador completo:

- Contactos.
- Compañías.
- Actividades CRM.
- Tareas.
- Comentarios de timeline.
- Catálogo normalizado de campos personalizados.
- Valores de campos personalizados en `bitrix.entity_custom_values`.

Limitaciones importantes:

- No hay autenticación de usuarios finales aplicada a todos los endpoints HTTP. Los endpoints `/api/admin/*` sí exigen `ADMIN_API_KEY` mediante el encabezado `X-Admin-Key`.
- La sincronización masiva usa una tarea en memoria; en producción debe migrarse a Cloud Run Jobs, Cloud Tasks o un Worker.
- La sincronización incremental no detecta negocios eliminados en Bitrix o movidos fuera de una pipeline. Se requiere una reconciliación completa ocasional.
- La selección de negocios actual trae campos de resumen. Los `UF_CRM_*` ya almacenados se conservan, pero una carga nueva no solicita todavía todo el catálogo de campos personalizados.
- El catálogo visual de informes y las cuatro pipelines principales están definidos también en JavaScript; agregar una pipeline en PostgreSQL no la agrega automáticamente a todas las pantallas.
- Algunos bloques del informe de Diego todavía son vistas iniciales o reutilizan agregaciones generales.

## Arquitectura

```text
┌──────────────┐
│   Bitrix24   │
└──────┬───────┘
       │ webhook REST: crm.deal.list, user.get, etapas
       ▼
┌──────────────────────────┐
│ API ASP.NET Core / .NET  │
│ cliente + sincronizadores│
└────────────┬─────────────┘
             │ Npgsql
             ▼
┌──────────────────────────┐
│ PostgreSQL / Cloud SQL   │
│ auth, bitrix, reporting  │
└────────────┬─────────────┘
             │ endpoints de consulta
             ▼
┌──────────────────────────┐
│ HTML + CSS + JavaScript  │
│ dashboards e informes   │
└──────────────────────────┘
```

Despliegue previsto en Google Cloud:

- API y frontend: Cloud Run.
- Base de datos: Cloud SQL for PostgreSQL.
- sincronizaciones programadas: Cloud Run Jobs o Scheduler + Tasks.
- secretos: Secret Manager.

## Estructura del repositorio

```text
backend/
  Bitrix/          Cliente, métodos, opciones y catálogo inicial de Bitrix
  Configuration/  Carga de variables desde .env
  Data/            Consultas que alimentan interfaces, administración e informes
  Reports/         Reglas de acceso a informes
  Security/        Códigos de permisos
  Sync/            Servicios y repositorio de sincronización
  wwwroot/         Interfaz HTML, CSS y JavaScript
  Program.cs       Inyección de dependencias y endpoints HTTP
db/
  migrations/      Esquema, índices, datos iniciales y pipelines
docs/
  architecture.md  Decisiones de arquitectura
  bitrix-sync.md   Estrategia detallada de sincronización
  database.md      Modelo de datos
```

## Configuración

Copiar `.env.example` como `.env` y reemplazar los valores de ejemplo:

```env
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Default=Host=HOST;Port=5432;Database=informes_avanzar;Username=USUARIO;Password=CONTRASEÑA
BITRIX_WEBHOOK_URL=https://INSTANCIA.bitrix24.es/rest/USUARIO_ID/TOKEN/
Bitrix__Scopes=crm,user
ADMIN_API_KEY=CLAVE_ADMINISTRATIVA_LARGA_Y_ALEATORIA
```

Reglas de seguridad:

- `.env` está ignorado por Git y nunca debe versionarse.
- No copiar webhooks, contraseñas o tokens en código, documentación, commits o capturas.
- En Cloud Run, usar variables de entorno o Secret Manager. Las variables del entorno tienen prioridad sobre `.env`.
- El webhook de Bitrix necesita como mínimo los scopes `crm` y `user` para lo implementado actualmente.

### Cloud SQL por IP pública

Usar el host público y el puerto estándar de PostgreSQL:

```text
Host=IP_PUBLICA;Port=5432;...
```

La IP de salida del equipo debe estar incluida como `/32` en las redes autorizadas de Cloud SQL. No autorizar `0.0.0.0/0`.

### Cloud SQL mediante proxy

Si se usa Cloud SQL Auth Proxy en el puerto local `5433`:

```text
Host=127.0.0.1;Port=5433;...
```

El proxy debe estar ejecutándose antes de iniciar la API y debe apuntar al nombre de conexión de la instancia.

## Ejecución local

### Con Docker

Copie `.env.example` como `.env` y cambie los valores sensibles antes de iniciar. La administración de usuarios requiere `ADMIN_API_KEY`.

```bash
cp .env.example .env
docker compose up --build
```

Esto levanta PostgreSQL 17 y la API en `http://localhost:8080`. Las migraciones dentro de `db/migrations` se ejecutan automáticamente solo cuando se crea un volumen nuevo de PostgreSQL.

### Sin Docker

Se requiere .NET 10 y una instancia PostgreSQL ya preparada:

```bash
dotnet restore backend/InformesAvanzar.Api.csproj
dotnet run --project backend/InformesAvanzar.Api.csproj
```

Validaciones básicas:

```text
GET /health
GET /health/db
GET /api/bitrix/config
GET /api/bitrix/test/users
```

Interfaces:

- `/`: inicio.
- `/informes.html`: catálogo visual de informes.
- `/reporte.html?id=rch_comercial`: informe de una pipeline.
- `/reporte.html?id=fuerza_comercial_diego`: dashboard especial.
- `/sincronizacion.html`: operación y seguimiento de sincronizaciones.
- `/usuarios.html`: administración inicial de usuarios, roles y accesos.

## Base de datos

PostgreSQL está dividido en cuatro schemas:

| Schema | Responsabilidad |
| --- | --- |
| `bitrix` | Conexión, pipelines, etapas, datos sincronizados, payloads e historial. |
| `reporting` | Definición, acceso, ejecuciones y snapshots de informes. |
| `auth` | Usuarios locales, roles y permisos. |
| `audit` | Eventos de auditoría. |

Las migraciones actuales son:

- `001_initial_schema.sql`: modelo completo, índices, roles, permisos y cuatro pipelines iniciales.
- `002_diego_report_pipelines.sql`: pipelines adicionales requeridas por el alcance del informe de Diego.
- `003_bitrix_departments.sql`: almacenamiento de departamentos sincronizados desde Bitrix.
- `004_report_definitions.sql`: definiciones iniciales de informes publicables y asignables.

En una base existente, una migración nueva debe aplicarse explícitamente. Docker no vuelve a ejecutar automáticamente scripts agregados después de haber creado el volumen.

## Pipelines configuradas

Pipelines principales:

| Slug | Nombre | `CATEGORY_ID` | Dominio |
| --- | --- | ---: | --- |
| `rch_comercial` | RCH Comercial | 8 | comercial |
| `rch_operativa` | RCH Operativa | 10 | operaciones |
| `pnnc_comercial` | PNNC Comercial | 26 | comercial |
| `pnnc_operativa` | PNNC Operativa | 28 | operaciones |
| `1116_comercial` | 1116 Comercial | 30 | comercial |
| `1116_operativa` | 1116 Operativa | 32 | operaciones |
| `informes_bi_builder` | Informes BI Builder | 224 | comercial |
| `lp_2445_operativa` | LP-2445 Operativa | 248 | operaciones |

Pipelines adicionales de la migración `002`:

| Slug | Nombre | `CATEGORY_ID` | Dominio |
| --- | --- | ---: | --- |
| `rch_cartera` | RCH Cartera | 12 | cartera |
| `pnnc_cartera` | PNNC Cartera | 68 | cartera |
| `cuentas_cobro` | Cuentas de Cobro | 72 | comercial |
| `informes_bi_builder` | Informes BI Builder | 224 | comercial |
| `cobro_juridico_rch` | Cobro Jurídico RCH | 302 | cartera |
| `cobro_juridico_pnnc` | Cobro Jurídico PNNC | 308 | cartera |

El backend global obtiene las pipelines activas desde `bitrix.pipelines`; no necesita recompilarse para sincronizar una nueva pipeline insertada en esa tabla.

## Cómo agregar una pipeline

### 1. Confirmar la categoría en Bitrix

Obtener el `CATEGORY_ID` real de la pipeline y definir:

- `slug`: identificador estable, en minúsculas y sin espacios.
- `name`: nombre visible.
- `domain`: área funcional, por ejemplo `comercial`, `operaciones`, `cartera` o `juridico`.
- `sync_order`: orden de ejecución global.

### 2. Crear una migración

No editar una migración ya aplicada. Crear, por ejemplo, `db/migrations/003_nueva_pipeline.sql`:

```sql
BEGIN;

INSERT INTO bitrix.areas (slug, name, description)
VALUES ('nueva_area', 'Nueva área', 'Descripción funcional.')
ON CONFLICT (slug) DO NOTHING;

INSERT INTO bitrix.pipelines (
    slug,
    name,
    category_id,
    domain,
    area_id,
    sync_order,
    is_active
)
SELECT
    'nueva_pipeline',
    'Nueva Pipeline',
    999,
    'nueva_area',
    a.id,
    110,
    true
FROM bitrix.areas a
WHERE a.slug = 'nueva_area'
ON CONFLICT (slug) DO UPDATE
SET name = EXCLUDED.name,
    category_id = EXCLUDED.category_id,
    domain = EXCLUDED.domain,
    area_id = EXCLUDED.area_id,
    sync_order = EXCLUDED.sync_order,
    is_active = true;

COMMIT;
```

El `category_id` es único. No reutilizar el mismo ID para dos pipelines.

### 3. Aplicar la migración

Aplicarla a cada ambiente: local, pruebas y Cloud SQL. Verificar después:

```sql
SELECT slug, name, category_id, domain, is_active
FROM bitrix.pipelines
ORDER BY sync_order;
```

### 4. Sincronizar sus etapas

```text
POST /api/bitrix/sync/stages
```

Este endpoint consulta Bitrix e inserta o actualiza automáticamente `bitrix.pipeline_stages` para todas las pipelines activas.

### 5. Realizar la carga inicial

```text
POST /api/bitrix/sync/deals/nueva_pipeline
```

La carga inicial completa crea el cursor necesario para futuras sincronizaciones incrementales. Si la pipeline es muy grande, puede cargarse operacionalmente por etapas, pero una colección parcial de etapas no debe considerarse una línea base completa.

### 6. Usar sincronización incremental

Después de una carga completa exitosa:

```text
POST /api/bitrix/sync/deals/nueva_pipeline/incremental
```

### 7. Agregarla a la interfaz

Actualmente se debe actualizar manualmente:

- `backend/wwwroot/sincronizacion.js`, arreglo `pipelines`.
- `backend/wwwroot/informes.js`, arreglo `dashboards`, si tendrá tarjeta propia.
- `backend/wwwroot/reporte.js`, objeto `metadata`, si usará la vista genérica.
- `backend/Bitrix/BitrixPipelineCatalog.cs`, solo si también debe aparecer en el endpoint de catálogo por defecto.

Como mejora futura, estos catálogos deberían generarse directamente desde `/api/bitrix/pipelines` y PostgreSQL.

## Cómo agregar o actualizar etapas

Las etapas no deben agregarse manualmente una por una cuando ya existen en Bitrix.

1. Crear o modificar la etapa en Bitrix.
2. Ejecutar `POST /api/bitrix/sync/stages`.
3. Confirmar el resultado en `bitrix.pipeline_stages`.
4. Recargar el informe.

Consulta de verificación:

```sql
SELECT p.slug, s.bitrix_stage_id, s.name, s.sort_order, s.status_type
FROM bitrix.pipeline_stages s
JOIN bitrix.pipelines p ON p.id = s.pipeline_id
WHERE p.slug = 'nueva_pipeline'
ORDER BY s.sort_order, s.name;
```

La distribución genérica por etapas usa esta tabla automáticamente. Un informe especial puede requerir además agregar el slug a su mapeo de frontend o modificar su consulta SQL.

## Cómo agregar un informe

Hay dos niveles distintos en la implementación actual.

### Informe genérico de pipeline

Para mostrar una pipeline usando las consultas generales:

1. Agregar su tarjeta a `backend/wwwroot/informes.js`.
2. Agregar nombre, área y descripción a `metadata` en `backend/wwwroot/reporte.js`.
3. Abrir `/reporte.html?id=slug_de_pipeline`.

La vista genérica consume:

- `/api/data/sync-summary?pipeline=slug`.
- `/api/data/deals?pipeline=slug`.
- `/api/data/stage-distribution?pipeline=slug`.
- `/api/data/responsible-distribution?pipeline=slug`.

### Informe especializado

Para cálculos propios, como `Fuerza Comercial Diego`:

1. Crear métodos de consulta parametrizados en `backend/Data/BitrixDataQueries.cs`.
2. Publicar endpoints en `backend/Program.cs`.
3. Agregar la tarjeta en `backend/wwwroot/informes.js`.
4. Agregar metadata, secciones y funciones de renderizado en `backend/wwwroot/reporte.js`, o crear una página separada si el diseño lo requiere.
5. Documentar pipelines, etapas y códigos `UF_CRM_*` utilizados.
6. Probar años, valores nulos, responsables sin usuario asociado y pipelines sin datos.

Si se activa el catálogo dinámico de informes, crear también un registro en `reporting.report_definitions` y asignar acceso mediante `reporting.report_access`.

Ejemplo mínimo:

```sql
INSERT INTO reporting.report_definitions (
    code,
    name,
    description,
    query_key,
    status
)
VALUES (
    'nuevo_informe',
    'Nuevo informe',
    'Descripción del informe.',
    'nuevo_informe',
    'published'
)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name,
    description = EXCLUDED.description,
    query_key = EXCLUDED.query_key,
    status = EXCLUDED.status;
```

Actualmente el catálogo visual de `informes.js` no lee automáticamente `reporting.report_definitions`; ambos deben mantenerse coordinados.

## Modos de sincronización

### Completa

Usarla para:

- Carga inicial de una pipeline.
- Reconciliación periódica.
- Recuperar datos que pudieron salir del alcance incremental.

Endpoints:

```text
POST /api/bitrix/sync/deals/{pipelineSlug}
POST /api/bitrix/sync/deals/{pipelineSlug}?stageId={stageId}
POST /api/bitrix/sync/deals/{pipelineSlug}?fullHistory=true
POST /api/bitrix/sync/deals/{pipelineSlug}?fullHistory=true&coreFieldsOnly=true
POST /api/bitrix/sync/deals/{pipelineSlug}?fullHistory=true&coreFieldsOnly=true&resumeFrom=20000
POST /api/bitrix/sync/global
POST /api/bitrix/sync/massive
```

La persistencia se confirma por página, no al final de toda la pipeline. Si el proceso falla después de varias páginas, los bloques anteriores permanecen guardados.

Por defecto, una sincronización completa consulta negocios creados desde 2025. Use
`fullHistory=true` solamente cuando un informe deba reconstruirse con todo el histórico;
esta operación puede tardar varios minutos en pipelines grandes.
Agregue `coreFieldsOnly=true` para reconstruir rápidamente inventarios y embudos con
identidad, etapa, responsable, valor y fechas. Los campos personalizados existentes
se conservan; una sincronización normal posterior puede enriquecer los registros nuevos.
`resumeFrom` acepta un desplazamiento múltiplo de 50 para continuar una carga
interrumpida. Una continuación no reconcilia eliminados porque no recorre las páginas
anteriores; esa reconciliación se realiza solamente en una carga completa desde cero.

### Incremental

Usarla para la operación diaria. Consulta negocios modificados desde el último cursor exitoso mediante `DATE_MODIFY`, con dos minutos de solapamiento para proteger el límite temporal.

Endpoints:

```text
POST /api/bitrix/sync/deals/{pipelineSlug}/incremental
POST /api/bitrix/sync/global/incremental
```

El hash del payload evita volver a escribir una versión idéntica. Una pipeline sin carga completa válida no puede iniciar incrementalmente porque no existe una línea base confiable.

### Usuarios y etapas

```text
POST /api/bitrix/sync/users
POST /api/bitrix/sync/departments
POST /api/bitrix/sync/stages
```

La sincronización global completa incluye usuarios y etapas. La global incremental sincroniza solamente negocios.

## Endpoints principales de consulta

```text
GET /api/data/sync-state
GET /api/data/sync-history
GET /api/data/sync-summary?pipeline={slug|all}
GET /api/data/deals?pipeline={slug|all}
GET /api/data/stages?pipeline={slug|all}
GET /api/data/stage-distribution?pipeline={slug|all}
GET /api/data/responsible-distribution?pipeline={slug|all}
GET /api/data/users
GET /api/admin/access-management
GET /api/reports/fuerza-comercial-diego/valores-radicados?year=2026
GET /api/reports/fuerza-comercial-diego/dashboard?year=2026
GET /api/reports/fuerza-comercial-diego/cartera-recaudada?year=2026
GET /api/reports/fuerza-comercial-diego/liderazgo-comisiones?year=2026
POST /api/bitrix/sync/reports/comercial/commissions?year=2026
POST /api/bitrix/sync/reports/comercial/portfolio-state
```

## Informe Fuerza Comercial Diego

Este dashboard combina negocios, snapshots, usuarios, pipelines y etapas.

Datos relevantes:

- Pipelines comerciales: categorías 8 y 26.
- Pipelines operativas: categorías 10 y 28.
- Año comercial: `UF_CRM_1737654190`.
- Año operativo: `UF_CRM_1737653376`.
- Mes de radicación: `UF_CRM_1676419915`.

Los valores enumerados de Bitrix se comparan tanto por ID como por texto en algunas consultas. Si Bitrix cambia los IDs de las opciones, se deben actualizar los `CASE` dentro de `BitrixDataQueries.cs`.

## Operación y diagnóstico

Antes de sincronizar:

1. Verificar `GET /health/db`.
2. Verificar `GET /api/bitrix/config`.
3. Confirmar que no haya una ejecución activa en `GET /api/data/sync-state`.
4. Confirmar scopes del webhook.

Durante la sincronización:

- Revisar `recordsRead` y `recordsWritten`.
- No iniciar otra sincronización mientras haya una ejecución activa.
- No matar el proceso si es posible; podría dejar un run marcado como `running` hasta la recuperación automática.
- Los bloques ya confirmados permanecen guardados aunque falle una página posterior.

Después:

- Confirmar `status = succeeded` en el historial.
- Revisar el mensaje de error si terminó en `failed`.
- Recargar el informe correspondiente.

Consultas útiles:

```sql
SELECT entity_type, mode, status, records_read, records_written,
       started_at, finished_at, cursor_value, error_message
FROM bitrix.sync_runs
ORDER BY created_at DESC
LIMIT 20;

SELECT p.slug, count(*) AS deals, max(d.bitrix_updated_at) AS last_bitrix_change
FROM bitrix.deals d
JOIN bitrix.pipelines p ON p.id = d.pipeline_id
GROUP BY p.slug
ORDER BY p.slug;
```

## Flujo recomendado de desarrollo

1. Crear una rama de trabajo.
2. Agregar cambios de esquema mediante una migración nueva.
3. Mantener consultas SQL parametrizadas.
4. Compilar:

   ```bash
   dotnet build backend/InformesAvanzar.Api.csproj
   ```

5. Probar salud de API y base.
6. Probar primero una pipeline o etapa pequeña.
7. Verificar conteos y payloads en PostgreSQL.
8. Probar el informe en el navegador.
9. No versionar `.env`, logs, tokens ni contraseñas.

## Próximos pasos recomendados

1. Solicitar y normalizar todos los campos `UF_CRM_*` necesarios para informes.
2. Mover trabajos largos a Cloud Run Jobs o un Worker persistente.
3. Implementar sincronizadores de contactos, compañías, actividades, tareas y comentarios.
4. Leer pipelines e informes desde PostgreSQL en lugar de mantener catálogos duplicados en JavaScript.
5. Implementar autenticación y aplicar permisos a todos los endpoints.
6. Añadir pruebas automatizadas para paginación, cursores, hashes y transformaciones.
7. Implementar reconciliación de eliminados y negocios movidos de pipeline.
8. Añadir un proceso formal de actualización/materialización de informes si el volumen hace costosas las consultas en vivo.

## Documentación complementaria

- `docs/architecture.md`: arquitectura y decisiones para Google Cloud.
- `docs/bitrix-sync.md`: detalles de la estrategia de sincronización.
- `docs/database.md`: tablas, schemas y convenciones de base de datos.
