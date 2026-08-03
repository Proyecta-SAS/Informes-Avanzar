# Arquitectura

## Principios

- Separar datos crudos de Bitrix, datos normalizados y datos listos para informes.
- Usar PostgreSQL como fuente principal de verdad para usuarios, roles, permisos, informes y estado de sincronizacion.
- Mantener auditoria de accesos y acciones relevantes desde el primer dia.
- Preparar la base para crecimiento: indices, claves foraneas, historial de sincronizacion y tablas particionables.

## Componentes en Google Cloud

### Cloud Run

Servicio principal para la aplicacion web y API. La API inicial esta planteada en ASP.NET Core, empaquetada en Docker para desplegarse en Cloud Run.

### Cloud SQL for PostgreSQL

Base de datos relacional principal. Se propone PostgreSQL porque permite:

- Relaciones estrictas y claves foraneas.
- JSONB para conservar respuestas originales de Bitrix.
- Indices avanzados.
- Row Level Security si luego se decide reforzar permisos a nivel de base de datos.
- Particionamiento para tablas grandes.

### Cloud Run Jobs

Procesos programados para sincronizar informacion desde Bitrix. Se recomienda implementarlos como Worker Services en .NET para reutilizar modelos, configuracion y acceso a base de datos:

- Sincronizacion completa inicial.
- Sincronizacion incremental.
- Reintentos de lotes fallidos.
- Reconstruccion de tablas de reporte.

### Cloud Scheduler y Cloud Tasks

Scheduler dispara sincronizaciones periodicas. Tasks permite dividir trabajo pesado en lotes para no saturar Bitrix ni la base de datos.

### Secret Manager

Almacena credenciales de Bitrix, cadenas de conexion y secretos de autenticacion.

### Cloud Storage

Guarda archivos exportados, adjuntos temporales o snapshots de informes cuando aplique.

## Flujo de datos

1. Un job consulta Bitrix por entidad: negocios, contactos, companias, actividades, usuarios u otras fuentes.
2. La respuesta original se guarda en `bitrix.raw_payloads` para trazabilidad.
3. Los datos importantes se transforman a tablas canonicas dentro del schema `bitrix`.
4. Los informes se definen en `reporting.report_definitions`.
5. El acceso se controla mediante `auth.users`, `auth.roles`, `auth.permissions` y reglas de acceso a informes.
6. Cada ejecucion y acceso queda registrado en tablas de auditoria.

## Modelo de permisos

El sistema combina dos niveles:

- **Permisos funcionales:** definen acciones como administrar usuarios, ver informes o ejecutar sincronizaciones.
- **Acceso a informes:** define que usuario o rol puede ver un informe especifico.

Esto evita depender solo de roles globales. Por ejemplo, una persona puede tener permiso para ver informes, pero solo acceder a los informes que le fueron asignados.

## Estrategia Bitrix

Se recomienda iniciar con estas entidades:

- Usuarios Bitrix.
- Deals o negocios.
- Contactos.
- Companias.
- Actividades.
- Campos personalizados.

La tabla `bitrix.raw_payloads` conserva la respuesta completa, mientras que las tablas canonicas guardan campos operativos e indexables. Esto permite retransformar datos sin volver a consumir la API de Bitrix.

## Seguridad

- Todas las credenciales deben estar en Secret Manager.
- Ningun token de Bitrix debe persistirse en codigo ni archivos versionados.
- La API debe validar usuario autenticado y permisos en cada endpoint.
- Las tablas de auditoria deben registrar accesos a informes y acciones administrativas.
- Los informes sensibles deben validar acceso por usuario o rol, no solo por URL.

## Escalabilidad

- Usar paginacion y lotes para sincronizacion.
- Guardar cursores de sincronizacion por entidad.
- Indexar claves externas de Bitrix.
- Separar transformaciones pesadas en jobs.
- Considerar particionar tablas de payloads y eventos por fecha cuando el volumen crezca.
