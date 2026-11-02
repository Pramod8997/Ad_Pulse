# AdPulse Phase 1 handoff

Date: 2026-11-03  
Status: foundation implementation complete; live SQL Server and OIDC-provider checks are environment-blocked.

## 1. Frozen reference

- Frozen foundation reference commit: `475a72bc67dbf9cfbcfac2126afa8423aa7099f7`
- The handoff document is updated in the follow-up documentation commit after that frozen foundation.
- Ten bounded agents ran in parallel for domain, scoring, identity, EF, SQL, API identity, Python scoring, contracts, isolation tests and build/CI review. The host did not run all 20 logical specialist roles.

## 2. Toolchain and commands

Observed on Windows `win-x64`:

- .NET SDK `8.0.427`, MSBuild `17.11.69`
- Python `3.10.11`
- FastAPI `0.162.0`, Pydantic `2.14.1`, pytest `9.1.1`
- Node `v24.20.0`, npm `11.19.0`
- Git `2.55.0.windows.5`
- Docker unavailable

Commands actually run from the repository root:

```text
.tools\dotnet\dotnet.exe restore AdPulse.sln --verbosity minimal       PASS
.tools\dotnet\dotnet.exe build AdPulse.sln --configuration Release --no-restore --verbosity minimal
  PASS: 0 warnings, 0 errors
.tools\dotnet\dotnet.exe test AdPulse.sln --configuration Release --no-build --no-restore
  PASS: Domain 21, Infrastructure 13, API 13; 47 passed, 0 failed
python -m pytest -q                                             PASS: 13 passed
python -m compileall -q adpulse_scoring tests                    PASS
JSON contract parse and four-fixture load                         PASS
SQL migration static assertions                                   PASS
```

GitHub Actions was configured in `.github/workflows/dotnet.yml` but was not executed locally.

## 3. Accepted contracts

- Database/schema version: `db/migrations/001_initial.sql` (`001_initial`), with the matching EF model in `src/AdPulse.Infrastructure/Data/AdPulseDbContext.cs`.
- API/DTO contract: `api-v1`, documented in `contracts/api-v1.md`. Phase 1 exposes only health in ASP.NET; workflow routes are reserved for Phase 2.
- Scoring request/response contract: `scoring-v1`, defined by `contracts/scoring-v1.schema.json`, implemented by the .NET and Python engines, and locked by `contracts/scoring-v1-fixtures.json`.
- UI contract: `docs/ui-contract.md`, aligned with `14-frontend-design-system.md`; it freezes routes, token strategy and client ownership without implementing screens.

## 4. Data and KPI policy

- CTR = clicks / impressions.
- CPC = spend / clicks.
- CPA = spend / conversions.
- ROAS = revenue / spend.
- Budget pacing = spend / planned-to-date budget.
- Every ratio uses decimal arithmetic. A zero denominator returns `null`; no Infinity or NaN is emitted.
- Money uses SQL Server `decimal(19,4)` and .NET/Python decimal semantics. Scoring uses four-place half-up rounding and caps the score at 100.
- A campaign has one three-letter uppercase currency. Cross-currency aggregation is deferred until the Phase 2 API contract supports it.
- Daily metric dates are tenant reporting-calendar dates. Ingestion must convert from the tenant timezone before persistence; Phase 1 has no timezone-settings endpoint and therefore does not silently choose a machine timezone.

## 5. Tenant and identity design

- Production requests use OIDC bearer validation for issuer, audience, signature and lifetime with zero clock skew; JWT inbound claim mapping is disabled so `sub` and `tenant_id` retain their contract names.
- The tenant resolver requires exactly one non-blank `sub` and `tenant_id`, then checks server-side membership. Client headers, route values, query values and request-body tenant IDs are not authorization inputs.
- EF contexts receive a `TenantScope`; query filters constrain all tenant-owned entities. `SaveChanges` rejects cross-tenant writes. Composite tenant-aware foreign keys provide a SQL boundary for associations.
- Negative tests cover missing/duplicate/malformed claims, mismatched membership, wrong issuer, wrong audience, expired/no-expiry tokens, invalid/unsigned signatures, unsafe authority URLs and weak test keys.
- Tenant isolation tests cover membership, campaigns, daily metrics, imports, import row errors, alerts and audit events for reads and writes. The daily metric key is idempotent per tenant/campaign/date; alert fingerprints are unique per tenant; audit events are append-only in application checks and the SQL trigger.

## 6. Scoring behavior

- `scoring-v1` is stateless and deterministic. It returns an explainable reason code, data sufficiency, confidence, severity and stable SHA-256 fingerprint.
- Below the minimum data threshold: no alert, `insufficient_data`.
- Zero baseline: no alert, `baseline_unavailable`.
- Healthy values: `within_baseline`.
- Degradation is relative deviation capped at 100, mapped to none/low/medium/high/critical. Confidence is data sufficiency, not a probability.
- Both implementations pass the shared golden fixtures.

## 7. Phase 1 exit-gate evidence

| Gate | Result |
|---|---|
| Implemented projects build cleanly | Pass: 0 warnings, 0 errors |
| Cross-tenant read/write coverage | Pass in EF SQLite integration tests; SQL Server execution blocked |
| Identity issuer/audience/expiry behavior | Pass in local signed-token tests; live provider metadata blocked |
| SQL tenant-aware association constraints | Pass by EF/SQLite and static migration review; SQL Server execution blocked |
| Decimal arithmetic and zero-denominator semantics | Pass: 21 domain tests |
| Repeated metric ingestion/scoring idempotency | Pass: composite metric key, unique alert fingerprint and deterministic fixtures |
| Deterministic explainable scoring | Pass: shared fixtures and .NET/Python tests |
| Critical design conflicts | None; ADRs 0001–0005 accepted |
| Handoff evidence | This document records actual commands, outcomes and blockers |

## 8. Known blockers and deferred scope

- No SQL Server, `sqlcmd`, LocalDB or Docker is installed. The SQL script was statically reviewed but not executed against SQL Server.
- No OIDC provider configuration or discovery endpoint is available. Provider-bound tests use a local symmetric key and do not prove an external issuer's metadata or key rotation behavior.
- ASP.NET currently maps only `/health`. Tenant context/EF wiring, campaign and metric workflows, CSV imports, alert persistence/lifecycle, audit APIs and problem-details middleware remain Phase 2 work.
- The Python scorer is an internal stateless service without authorization middleware. Phase 2 must put it behind the service boundary and integrate it through a bounded typed client; it must not authorize tenants.
- Vue screens, browser/BDD tests, Docker Compose, operational logging/ELK, deployment documentation and the full README are Phase 2 scope.

## 9. Phase 2 dependency order

1. Verify this handoff and `475a72bc67dbf9cfbcfac2126afa8423aa7099f7`; provision SQL Server and OIDC test configuration.
2. Wire bearer authentication, tenant membership resolution, tenant-scoped DbContext creation, correlation/problem details and protected API authorization tests.
3. Implement campaign CRUD, daily metric ingestion, pagination and import validation against `api-v1`; retain the negative isolation suite.
4. Integrate the Python `scoring-v1` client with timeout/retry semantics, persist idempotent alerts, and implement alert lifecycle/audit writes.
5. Build the Vue shell and routes from `docs/ui-contract.md`, then implement real API-backed screens and all loading/empty/error/partial/stale/accessibility states.
6. Add observability, local Docker profile, clean-clone README/setup and CI checks for the completed workflows.
7. Finish BDD/browser smoke, performance/security review, independent release review and the final Phase 2 handoff.
