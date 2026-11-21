# AdPulse operations

This is the Phase 2 local operations baseline. It keeps the API as the authorization boundary, keeps the scorer stateless, and never puts credentials or bearer tokens in logs or CI.

## Health and readiness

The API exposes an anonymous `GET /health` liveness check. A `200` response means the API process can accept requests; it does not prove that SQL Server, migrations, OIDC discovery, or the scorer are available.

The scorer exposes its own anonymous `GET /health` endpoint. SQL Server readiness remains an environment check because this repository does not run migrations automatically.

The Compose SQL Server healthcheck proves only that the SQL Server engine accepts a local query. It does not apply the ordered migration chain.

## Structured logs and correlation

`src/AdPulse.Api/Observability/` contains dormant integration hooks so the host can opt in without changing the current API startup:

```csharp
builder.Services.AddAdPulseObservability();

// After building the app, before endpoint mappings:
app.UseAdPulseObservability();
```

`UseAdPulseObservability` accepts a safe `X-Correlation-ID` value or generates one, returns it on the response, and places it in the logging scope as `correlation_id`. It logs only method, path (without the query string), status, duration, and exception type. Invalid or control-character-containing correlation values are replaced.

For workflow-specific fields, inject `IRedactedLogSanitizer` and sanitize properties before passing them to a logger. Sensitive names such as authorization, token, secret, password, cookie, API key, connection string, SQL/query, payload, raw, and body are replaced with `[REDACTED]`. Unknown object values are also redacted and strings are bounded to 256 characters.

Do not log access tokens, cookies, connection strings, SQL text, stack traces to user-facing output, uploaded rows, request bodies, or tenant data that is not required to diagnose the operation. Prefer stable identifiers and counts. The sanitizer is an opt-in extension point; it does not intercept every `ILogger` call, so new log statements must use it deliberately.

## Docker local profile

Docker is not installed in the Phase 1 environment. On a machine with Docker Compose, start the source-mounted local profile with:

```text
docker compose --profile local up
```

The profile starts services in this order: SQL Server engine, scorer, then API. It publishes API on `http://localhost:8080`, scorer on `http://localhost:8000`, and SQL Server on `localhost:1433`. The default SQL password is development-only; override it before starting a shared machine:

```text
# POSIX shells
MSSQL_SA_PASSWORD="use-a-local-development-password" docker compose --profile local up

# PowerShell
$env:MSSQL_SA_PASSWORD = "use-a-local-development-password"
docker compose --profile local up
```

Create the local database and apply the ordered checked-in migrations once SQL Server is healthy:

```text
docker compose --profile local exec sqlserver sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "IF DB_ID(N'"'"'AdPulse'"'"') IS NULL CREATE DATABASE [AdPulse]"'
docker compose --profile local exec sqlserver sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/001_initial.sql'
docker compose --profile local exec sqlserver sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/002_tenant_reporting_settings.sql'
docker compose --profile local exec sqlserver sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/003_import_pending_metrics.sql'
docker compose --profile local exec sqlserver sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d AdPulse -i /workspace/db/migrations/004_alert_incident_workflow.sql'
```

The API container passes the SQL connection and scorer base address through environment variables. The API uses the SQL Server database and typed scorer client for the existing workflow services.

Stop the profile with `docker compose --profile local down`. The named `sqlserver-data` volume preserves local data; remove it only when intentionally resetting the database.

## Docker-unavailable fallback

The API and scorer can run natively, but this repository's Phase 1 environment has no SQL Server, LocalDB, or Docker. Run the scorer from `scoring` with Python 3.10 and an installed `uvicorn`:

```text
python -m pip install -e . "uvicorn>=0.30,<1"
python -m uvicorn adpulse_scoring.app:app --host 127.0.0.1 --port 8000
```

Run the API from the repository root with the installed .NET 8 SDK (the Phase 1 Windows checkout uses `.tools\dotnet\dotnet.exe`):

```text
.tools\dotnet\dotnet.exe run --project src\AdPulse.Api\AdPulse.Api.csproj --urls http://127.0.0.1:8080
```

Without an externally provisioned SQL Server, database-backed API workflows and the readiness check cannot be exercised. Do not substitute SQLite for the SQL Server contract; use the existing SQLite isolation tests for local code-level checks and record SQL Server execution as blocked.

## CI

`.github/workflows/phase2.yml` runs .NET restore/build/test, Python install/test/compile, and the checked-in `web/` frontend typecheck/build with read-only repository permissions and no secrets. The existing `.github/workflows/dotnet.yml` remains the Phase 1 baseline workflow.

CI does not require SQL Server, OIDC credentials, Docker, or an ELK stack. Provider-bound and database-runtime checks remain environment-dependent until a separate test service/configuration is authorized.
