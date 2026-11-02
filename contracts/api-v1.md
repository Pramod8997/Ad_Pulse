# AdPulse API contract v1

Status: accepted for Phase 1. The contract records the implemented health/scoring boundaries and the security rules that product workflows must use. Product workflow endpoints remain Phase 2.

## Components and trust boundary

### ASP.NET API

- The host is configured for `Authorization: Bearer <OIDC access token>` using the configured OIDC authority.
- JWT bearer validation requires the configured issuer, audience and lifetime; the production bearer handler obtains signing metadata from the authority and uses zero clock skew.
- Phase 1 maps only the anonymous `GET /health` endpoint. No protected workflow endpoint is currently mapped, so no request currently creates a tenant scope through the ASP.NET host.
- The tenant foundation requires exactly one non-blank `sub` and one `tenant_id` claim, then checks `(subject, tenant)` membership in SQL Server. Roles come from membership rows. The resolver and tenant-scoped EF context are implemented and tested independently of the not-yet-wired workflow host.
- No request body, query string, route value or custom tenant header may select the authorization tenant. A scorer request's `tenant_id` is input data and is never an authorization decision.

### Python scoring service

- `GET /health` is anonymous and returns `{ "status": "ok" }`.
- `POST /v1/score` accepts the `scoring-v1` request and returns its deterministic response. The endpoint currently has no authentication middleware and must remain an internal service boundary; it does not resolve tenants, read memberships or grant authorization.
- Requests whose declared `Content-Length` exceeds 64 KiB are rejected with status `413` before Pydantic parsing. The scorer is stateless and does not persist alerts.

## Error behavior implemented in Phase 1

- There is no shared `application/problem+json` envelope in the Phase 1 implementation.
- The FastAPI scorer returns its framework JSON validation response for malformed requests (normally `422`) and a JSON `detail` response for the 64 KiB rejection (`413`). The ASP.NET health route has no workflow error surface.
- Future workflow errors must not expose stack traces, SQL, tokens, secrets or uploaded row contents. A problem-details shape and correlation policy must be defined before workflow routes are exposed.

## Data conventions

- Scoring dates are ISO-8601 calendar dates (`YYYY-MM-DD`) and the scorer rejects an evidence window whose end precedes its start. The tenant reporting-calendar conversion policy is recorded in ADR-0003; Phase 1 has no ingestion or timezone-settings endpoint.
- Where a future API exposes timestamps, the contract is UTC ISO-8601. Current persistence models use `DateTimeOffset`, but Phase 1 does not expose a timestamp serialization endpoint or normalize offsets at an API boundary.
- Scoring decimal inputs are canonical JSON strings with non-negative decimal syntax as defined by `contracts/scoring-v1.schema.json`; JSON number forms are rejected. Missing values are not zero.
- Campaign budget, metric spend and metric revenue use SQL Server `decimal(19,4)` and .NET `decimal`. Currency is stored as three uppercase characters; the current database checks that shape, not membership in an ISO-4217 reference table.
- KPI division by zero returns `null`. The implemented formulas are CTR = clicks / impressions, CPC = spend / clicks, CPA = spend / conversions, ROAS = revenue / spend and budget pacing = spend / planned-to-date budget.
- No Phase 1 list endpoint has pagination. Future list routes must define stable ordering and a bounded cursor contract before implementation.

## Phase 1 route surface

| Component | Route | Auth | Status and purpose |
|---|---|---|---|
| ASP.NET API | `GET /health` | anonymous | implemented process health only |
| Python scorer | `GET /health` | internal boundary | implemented scorer health |
| Python scorer | `POST /v1/score` | internal boundary | implemented scoring contract endpoint |
| ASP.NET API | `GET /app` | tenant member | reserved; not mapped in Phase 1 |
| ASP.NET API | `GET /app/campaigns` | tenant member | reserved; not mapped in Phase 1 |
| ASP.NET API | `GET /app/alerts` | tenant member | reserved; not mapped in Phase 1 |

## Scoring endpoint contract

`POST /v1/score` uses `application/json` and the request schema in `contracts/scoring-v1.schema.json`. `scoring_version` defaults to `scoring-v1` when omitted.

A successful response has status `200` and these fields:

| Field | Meaning |
|---|---|
| `score` | non-negative degradation score, rounded to four decimal places and capped at `100` |
| `severity` | `none`, `low`, `medium`, `high` or `critical` |
| `confidence` | data-sufficiency value from `0` to `1`, rounded to four decimal places; not a probability |
| `data_sufficiency` | `insufficient` or `sufficient` |
| `reason_codes` | one current explainability code: `insufficient_data`, `baseline_unavailable`, `within_baseline` or `degraded_vs_baseline` |
| `fingerprint` | lowercase 64-character SHA-256 fingerprint of the canonical scoring parts |

Scoring rules, thresholds and fingerprint construction are frozen in ADR-0004. Invalid request data receives the framework validation response; an oversized declared body receives `413`.

## Phase boundary

Campaign, metric-ingestion, import, alert-lifecycle, audit and settings routes; ASP.NET tenant-scope wiring; scorer orchestration/persistence; timezone configuration; generalized problem details; and pagination are deferred to Phase 2. No Phase 2 workflow implementation is described or implied by the Phase 1 route surface.
