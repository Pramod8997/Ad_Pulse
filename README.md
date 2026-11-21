# AdPulse

AdPulse is a tenant-scoped advertising analytics application. Phase 1 froze the database, API, scoring, identity, and UI contracts; Phase 2 adds the workflow API, Vue shell, local operations profile, and checks described in the [Phase 1 handoff](docs/phase-1-handoff.md).

## Current status

- ASP.NET exposes an anonymous `GET /health` endpoint and protected workflow controllers.
- The Python service exposes `GET /health` and `POST /v1/score`.
- The Vue frontend is under `web` and uses the frozen [UI contract](docs/ui-contract.md).
- SQL Server and an OIDC provider are required for the live database and authenticated workflow checks.

Read the [API contract](contracts/api-v1.md), [UI contract](docs/ui-contract.md), and [demo-data disclosure](docs/demo-data.md) before using the local data.

## Prerequisites

Install these tools and make them available on `PATH`:

- .NET SDK `8.0.427` (the repository pins this in `global.json`)
- Python `3.10` or newer
- Node.js `22` or newer and npm for the frontend checks
- SQL Server and Microsoft `sqlcmd` for database setup
- An OIDC provider for authenticated workflow checks

Docker Desktop is optional, but the `local` Compose profile is the shortest path to a local SQL Server, scorer, and API.

## Clean-clone setup (PowerShell)

From a new checkout:

```powershell
git clone <repository-url> adpulse
Set-Location adpulse

dotnet --version
python --version

dotnet restore AdPulse.sln --verbosity minimal
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -e "scoring[test]" "uvicorn>=0.30,<1"
Set-Location web
npm ci
Set-Location ..
```

If PowerShell blocks activation, run `Set-ExecutionPolicy -Scope Process Bypass` in that shell and activate again. Activation is optional; use `.venv\Scripts\python.exe` explicitly instead.

## Phase 2 run commands

Run these from the repository root after setup. The first group checks the Phase 1 foundation that Phase 2 builds on:

```powershell
dotnet restore AdPulse.sln --verbosity minimal
dotnet build AdPulse.sln --configuration Release --no-restore --verbosity minimal
dotnet test AdPulse.sln --configuration Release --no-build --no-restore
python -m pytest -q scoring/tests
python -m compileall -q scoring/adpulse_scoring scoring/tests
Set-Location web
npm run typecheck
npm run build
Set-Location ..
node --test web/tests/browser-smoke.test.mjs
```

The Phase 1 handoff records `.tools\dotnet\dotnet.exe` as the local verification path; a clean clone should use the pinned global SDK above because `.tools` is ignored by Git and may not exist.

### Start the local Docker profile (recommended)

Set a development-only SQL password in the current shell, then start the source-mounted local profile:

```powershell
$env:MSSQL_SA_PASSWORD = "use-a-local-development-password"
docker compose --profile local up -d
```

Apply the schema and demo data inside the healthy SQL Server container:

```powershell
docker compose --profile local exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$env:MSSQL_SA_PASSWORD" -Q "IF DB_ID(N'AdPulse') IS NULL CREATE DATABASE [AdPulse]"
docker compose --profile local exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$env:MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/001_initial.sql
docker compose --profile local exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$env:MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/002_tenant_reporting_settings.sql
docker compose --profile local exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$env:MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/003_import_pending_metrics.sql
docker compose --profile local exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$env:MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/004_alert_incident_workflow.sql
docker compose --profile local exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$env:MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/seed/001_demo.sql
curl.exe http://127.0.0.1:8080/health
```

Expected response: `{"status":"ok"}`. Stop the profile with `docker compose --profile local down`; the named SQL volume persists local data.

### Run the frontend

Start the Vite development server from `web`:

```powershell
Set-Location web
npm run dev -- --host 127.0.0.1
```

The browser smoke fallback expects `http://localhost:5173` unless `ADPULSE_BASE_URL` is set. Run it from another shell while the frontend is running:

```powershell
$env:ADPULSE_BASE_URL = "http://localhost:5173"
node --test web/tests/browser-smoke.test.mjs
```

Set `CHROME_PATH` when Chrome is installed somewhere other than its default Windows path.

### Run the services natively

For a Windows SQL Server using integrated authentication, apply the migration and seed as shown below, start the scorer, then configure the API. Use a real OIDC authority for authenticated calls; the example issuer is valid syntax for startup and is not an identity provider.

```powershell
$env:ADPULSE_SQL_SERVER = "localhost"
$env:ADPULSE_SQL_DATABASE = "AdPulse"
sqlcmd -S $env:ADPULSE_SQL_SERVER -d master -E -b -Q "IF DB_ID(N'AdPulse') IS NULL CREATE DATABASE [AdPulse];"
sqlcmd -S $env:ADPULSE_SQL_SERVER -d $env:ADPULSE_SQL_DATABASE -E -b -i db/migrations/001_initial.sql
sqlcmd -S $env:ADPULSE_SQL_SERVER -d $env:ADPULSE_SQL_DATABASE -E -b -i db/seed/001_demo.sql

python -m uvicorn adpulse_scoring.app:app --app-dir scoring --host 127.0.0.1 --port 8000
```

In another shell, set the API's non-secret local connection and provider settings before starting it:

```powershell
$env:ConnectionStrings__AdPulse = "Server=localhost;Database=AdPulse;Integrated Security=True;TrustServerCertificate=True"
$env:Scoring__BaseAddress = "http://127.0.0.1:8000"
$env:Identity__Authority = "https://issuer.example.invalid/"
$env:Identity__Audience = "adpulse-api"
dotnet run --project src/AdPulse.Api --urls http://127.0.0.1:8080
```

Check both services:

```powershell
curl.exe http://127.0.0.1:8000/health
curl.exe http://127.0.0.1:8080/health
```

`001_demo.sql` is the clearly labelled `[Demo data]` seed. It contains fictional tenant-scoped records only; see [docs/demo-data.md](docs/demo-data.md). The seed can be run again safely, but it does not update an existing record. The migration itself is a one-time script for a fresh database.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `dotnet` is not found or the version is not 8.0.427 | Install the .NET 8 SDK, or use the repository's provisioned `.tools\dotnet\dotnet.exe` if it exists. |
| `dotnet run` fails with `ConnectionStrings:AdPulse` | Set `ConnectionStrings__AdPulse` to the SQL Server connection string for the database that received the migration. |
| `dotnet run` fails with `Identity:Authority` or `Identity:Audience` | Set `Identity__Authority` to an absolute HTTP(S) issuer URL and `Identity__Audience` to a non-empty audience. Use HTTPS for a real provider. |
| The API starts but authenticated calls fail | The placeholder authority is suitable only for `/health`. Configure a reachable OIDC provider, matching issuer/audience, signing metadata, and a token containing exactly one valid `sub` and `tenant_id`. |
| `python -m uvicorn` is not found | Activate `.venv` and run `python -m pip install uvicorn`. |
| `docker compose` is not recognized | Install Docker Desktop, or use the native SQL Server/scorer/API commands in this README. |
| `npm ci` fails | Install Node.js 22 or newer, then rerun `npm ci` from `web`. |
| `sqlcmd` is not recognized | Install the Microsoft SQL command-line tools or run the same scripts through an approved SQL Server client. |
| Database creation or login fails | Confirm the SQL Server instance is running, the database name is correct, and the selected authentication mode has permission. Do not put a password in this repository. |
| The migration reports that an object already exists | It is intended for a fresh database. Point it at a new empty database or use the project's future migration process; do not rerun it over a partially initialized database. |
| The seed reports no changes | This is expected when its fixed demo IDs already exist. It is idempotent and does not overwrite changes. |
| Browser smoke tests are skipped | Set `ADPULSE_BASE_URL` to the running frontend URL; install Chrome or set `CHROME_PATH` if the default path is not available. |

## Repository map

- `src/AdPulse.Domain` — KPI, scoring, and tenancy primitives
- `src/AdPulse.Infrastructure` — EF model and tenant-isolation persistence rules
- `src/AdPulse.Api` — ASP.NET host and identity boundary
- `scoring` — stateless Python scoring service
- `db/migrations` — SQL Server schema
- `db/seed` — labelled fictional demo data
- `contracts` and `docs` — accepted API, scoring, UI, handoff, and data contracts
