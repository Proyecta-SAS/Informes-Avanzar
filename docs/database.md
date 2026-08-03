# Modelo de base de datos

## Schemas

### `auth`

Gestiona identidad, roles y permisos propios de la aplicacion.

Tablas principales:

- `auth.users`
- `auth.roles`
- `auth.permissions`
- `auth.role_permissions`
- `auth.user_roles`

### `bitrix`

Guarda informacion sincronizada desde Bitrix.

Tablas principales:

- `bitrix.connections`
- `bitrix.areas`
- `bitrix.pipelines`
- `bitrix.pipeline_stages`
- `bitrix.entity_types`
- `bitrix.custom_fields`
- `bitrix.entity_custom_values`
- `bitrix.entity_snapshots`
- `bitrix.sync_runs`
- `bitrix.raw_payloads`
- `bitrix.users`
- `bitrix.companies`
- `bitrix.contacts`
- `bitrix.deals`
- `bitrix.activities`
- `bitrix.tasks`
- `bitrix.timeline_comments`

### `reporting`

Define informes, permisos de acceso y ejecuciones.

Tablas principales:

- `reporting.report_definitions`
- `reporting.report_access`
- `reporting.report_runs`
- `reporting.report_snapshots`

### `audit`

Registra eventos de seguridad, accesos e integraciones.

Tablas principales:

- `audit.events`

## Convenciones

- Todas las tablas usan UUID como llave primaria.
- Las referencias externas de Bitrix se guardan en columnas `bitrix_id`.
- Las respuestas completas de Bitrix se guardan como `jsonb`.
- Los campos personalizados `UF_CRM_*` se conservan como JSONB y tambien como valores consultables cuando sean importantes para filtros o reportes.
- Todo registro importante tiene `created_at` y `updated_at`.
- Los borrados logicos usan `deleted_at` cuando el dato puede requerir auditoria.

## Estrategia para mucho volumen

`bitrix.raw_payloads` esta preparada para crecer y puede particionarse por `received_at`. En el arranque se deja como tabla normal para simplificar operacion; cuando el volumen lo exija, se migra a particiones mensuales.

Las consultas de informes no deben depender directamente de `raw_payloads`. Deben usar tablas canonicas o snapshots dentro de `reporting`.

## Replica mejorada de Bitrix

La base no copia Bitrix de forma plana. Usa tres capas:

- `bitrix.raw_payloads`: conserva la respuesta original completa de Bitrix.
- `bitrix.entity_snapshots`: snapshot generico de cualquier entidad sincronizada.
- Tablas canonicas: usuarios, companias, contactos, negociaciones, actividades, tareas y comentarios.

Las pipelines de Bitrix se interpretan como areas operativas del negocio mediante `bitrix.areas` y `bitrix.pipelines`. Esto permite que una misma aplicacion maneje cartera, juridico, comercial, operaciones u otras areas sin reescribir el modelo.

## Acceso a informes

`reporting.report_access` permite asignar un informe a:

- Un usuario especifico.
- Un rol completo.

La aplicacion debe calcular el acceso efectivo combinando:

- Usuario activo.
- Roles activos del usuario.
- Estado publicado del informe.
- Registros activos en `reporting.report_access`.
