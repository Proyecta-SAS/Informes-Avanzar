# Informes Avanzar

Aplicacion para centralizar informacion proveniente de Bitrix, transformarla en modelos de reporte y entregar informes controlados por usuario, rol y permisos.

## Objetivo

Construir una plataforma alojada en Google Cloud que permita:

- Sincronizar grandes volumenes de informacion desde Bitrix.
- Mantener una base de datos PostgreSQL ordenada por dominios.
- Crear usuarios, roles y permisos.
- Publicar informes personalizados para distintas personas o equipos.
- Auditar accesos, sincronizaciones y cambios relevantes.

## Arquitectura propuesta

- **App/API:** Cloud Run.
- **Base de datos:** Cloud SQL for PostgreSQL.
- **Jobs de sincronizacion:** Cloud Run Jobs o Cloud Scheduler + Cloud Tasks.
- **Secretos:** Secret Manager.
- **Archivos exportados:** Cloud Storage.
- **Observabilidad:** Cloud Logging, Error Reporting y alertas.

## Estructura inicial

```text
backend/
  Data/
  Reports/
  Security/
  Dockerfile
  InformesAvanzar.Api.csproj
  Program.cs
docs/
  architecture.md
  database.md
db/
  migrations/
    001_initial_schema.sql
```

## Desarrollo local

```bash
docker compose up --build
```

La API queda disponible en:

- `http://localhost:8080`
- `GET http://localhost:8080/health`
- `GET http://localhost:8080/health/db`

## Siguiente paso recomendado

1. Implementar autenticacion real.
2. Conectar la migracion a Cloud SQL.
3. Implementar el primer conector Bitrix.
4. Crear el primer informe con permisos por usuario y rol.

## Pipelines iniciales de sincronizacion

- RCH Comercial: categoria Bitrix `8`.
- RCH Operativa: categoria Bitrix `10`.
- PNNC Comercial: categoria Bitrix `26`.
- PNNC Operativa: categoria Bitrix `28`.
