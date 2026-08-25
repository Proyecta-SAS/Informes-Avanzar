# Repository Guidelines

## Project Structure & Module Organization

`backend/` contains the ASP.NET Core application targeting .NET 10. `Program.cs` configures dependency injection and HTTP endpoints; domain code is grouped into `Bitrix/`, `Sync/`, `Data/`, `Reports/`, `Security/`, and `Configuration/`. The browser UI is plain HTML, CSS, and JavaScript under `backend/wwwroot/`, with shared images in `wwwroot/assets/`. PostgreSQL schema changes live in `db/migrations/` and supporting design and operations notes live in `docs/`. Keep generated output, local logs, and secrets out of source control.

## Build, Test, and Development Commands

- `dotnet restore backend/InformesAvanzar.Api.csproj` restores NuGet packages.
- `dotnet build backend/InformesAvanzar.Api.csproj` compiles the API and catches nullable/type errors.
- `dotnet run --project backend/InformesAvanzar.Api.csproj` runs against an already configured PostgreSQL instance.
- `docker compose up --build` starts PostgreSQL 17 and the application at `http://localhost:8080`.
- `docker compose down` stops local containers without deleting the database volume.

Copy `.env.example` to `.env` before local execution. Check `/health` and `/health/db` after startup.

## Coding Style & Naming Conventions

Use four-space indentation in C# and follow existing .NET conventions: PascalCase for types, methods, and public members; camelCase for locals and parameters; and `I` prefixes for interfaces. Nullable reference types and implicit usings are enabled. Keep SQL parameterized and database identifiers in `snake_case`. Frontend filenames and pipeline/report slugs use lowercase kebab-case or established snake_case identifiers. Match nearby JavaScript and CSS formatting; no repository-wide formatter is currently configured.

## Testing Guidelines

There is currently no automated test project or coverage threshold. Every change must at minimum pass `dotnet build`. Exercise affected endpoints and UI pages locally, including empty data, null values, authorization failures, and database connectivity where relevant. If adding tests, create a separate test project, name test files `*Tests.cs`, and use descriptive behavior-oriented method names.

## Database & Security

Never edit an already-applied migration; add the next numbered, descriptive SQL file, for example `010_add_report_index.sql`. Remember that Docker initialization scripts run only for a new PostgreSQL volume. Never commit `.env`, Bitrix webhook URLs, API keys, passwords, or production data. Use environment variables or Secret Manager in deployed environments.

## Commit & Pull Request Guidelines

Recent commits commonly use release-prefixed summaries such as `V.1.3.37 Cambios filtro coordinadores`; use a concise imperative summary and include the version when preparing a release. Pull requests should explain the user-visible change, database/configuration impact, and verification performed. Link the relevant issue and include screenshots for UI changes. Call out new migrations and required environment variables explicitly.
