# AdPulse Product Evolution Audit

Date: 2026-11-04
Target positioning: **Advertising Operations & Intelligence Platform**

## Scope and baseline

This is an evolution of the existing AdPulse application. The repository remains the source of truth. The audit found a working modular .NET application, SQL Server persistence, stateless Python scoring service, and Vue frontend. The existing alert workflow is the natural starting point for incident operations.

Pre-existing working areas to preserve:

- Tenant-derived OIDC identity, server-side membership validation, EF tenant filters, and composite tenant keys.
- Campaign CRUD, daily metrics, decimal KPI semantics, CSV import staging/confirmation, and audit history.
- Deterministic scoring, stable fingerprints, evidence windows, severity, confidence, and idempotent alert persistence.
- Vue shell, overview, campaigns, campaign detail, alerts, imports, audit, settings, route guards, and design tokens.
- SQL Server migrations, local Compose profile, GitHub Actions, and existing unit/contract/browser checks.

Current verification evidence from the repository audit:

| Check | Result |
|---|---|
| `.tools\\dotnet\\dotnet.exe test AdPulse.sln --configuration Release --no-build --no-restore` | 160 passed |
| `python -m pytest -q scoring/tests` | 13 passed |
| `python -m compileall -q scoring/adpulse_scoring scoring/tests` | passed |
| `npm run typecheck` | passed |
| `npm run build` | passed |
| release-gate script | 25 passed; SQL Server/OIDC/browser gates skipped |
| browser smoke without `ADPULSE_BASE_URL` | 6 skipped |

The repository's default `dotnet`, Docker, and `sqlcmd` commands are unavailable in this environment; the pinned `.tools\\dotnet\\dotnet.exe` is available. SQL Server execution and authenticated browser workflows remain environment-dependent.

## Current feature inventory and classification

| Capability | Current implementation | Classification | Rationale / impact |
|---|---|---|---|
| Authentication and tenant resolution | JWT validation, claim-derived tenant, server membership lookup | KEEP | The security boundary is correct and must remain authoritative. |
| Tenant-scoped persistence | EF query filters, composite tenant FKs, SaveChanges guards | KEEP | Reuse for every incident read/write. |
| Campaign management | Campaign endpoints, forms, list/detail views | KEEP | Campaigns remain the operational context for incidents. |
| Daily metrics and KPI calculations | SQL metrics, decimal calculator, overview trends | KEEP | Operational intelligence depends on existing data and null semantics. |
| CSV imports | Parser, validation, staging, atomic confirmation | KEEP | Preserve ingestion; fix only proven contract/setup gaps separately. |
| Deterministic scoring | Python scorer, .NET client/contracts, fingerprints | KEEP | Do not change algorithm or authorization boundary. |
| Alert persistence and deduplication | Tenant/fingerprint uniqueness and evidence | KEEP | Existing alerts become the incident source record. |
| Alert list/detail and legacy actions | `/app/alerts`, acknowledge/resolve/reopen | EXTEND | Preserve routes and actions while adding incident operations. |
| Alert lifecycle representation | `open`, `acknowledged`, `resolved` | MODIFY | Keep legacy `status`; add an additive incident-state projection to avoid breaking consumers. |
| Incident ownership | Missing | ADD | Add same-tenant assignment/unassignment with server-side membership validation. |
| Investigation notes | Missing | ADD | Add bounded append-only notes through the existing audit/timeline store. |
| Incident timeline | Audit events exist but are not projected into alert details | EXTEND | Reuse append-only audit events; expose a safe alert-specific projection. |
| Mitigation / false-positive workflow | Missing | ADD | Add explicit validated transitions and required reason fields. |
| Resolution tracking | Reason only in audit payload; no queryable current metadata | EXTEND | Add nullable resolution summary/actor/time while keeping old resolved rows valid. |
| Operational intelligence overview | Existing KPIs, trends, pacing, priority alerts | EXTEND | Reframe existing overview and prioritize actionable alerts without replacing components. |
| Campaign-linked incident context | Alert has campaign relation; UI links are incomplete | EXTEND | Reuse campaign detail Alerts/Activity tabs; avoid causal claims. |
| Audit log | Tenant-scoped append-only events with safe generic response | EXTEND | Generalize writes and add safe timeline projection; never expose raw payload JSON. |
| API/UI contracts | Existing v1/v2 contracts and Vue adapters have drift | MODIFY | Add only the new incident fields/routes and align consumers. |
| SQL schema | `Alerts` and `AuditEvents` tables; migrations 001-003 | EXTEND | Add one additive incident migration; do not rewrite or reset the database. |
| Python scoring service | Stateless, deterministic, tested | KEEP | No incident persistence or authorization belongs there. |
| Deployment and operations docs | Local setup and migration order are incomplete/stale | MODIFY | Document the real migration chain and incident operational checks. |
| Existing tests and CI | Strong unit/contract baseline, limited real workflow/browser coverage | EXTEND | Add focused incident, tenant, redaction, and compatibility tests. |
| Separate `Incidents` aggregate | Not present; no proven multi-alert grouping requirement | RETIRE | Do not add a parallel aggregate until grouping is a demonstrated domain need. |
| Existing routes/features | Working campaign/import/audit/settings/scoring routes | KEEP | No route or feature is obsolete for this positioning. |

## Change-impact matrix

| Change | Files / tables / endpoints | Compatibility and security impact | Verification |
|---|---|---|---|
| Add incident projection to alert responses | `AlertContracts.cs`, alert service/endpoints, API v2 schema, Vue alert types | Existing `status` and legacy routes remain; new fields are additive and nullable. | Contract tests, alert service tests, frontend typecheck/build. |
| Add owner assignment | `Alert` mapping/migration, alert service/endpoints, alert API/view | Owner is validated against authenticated tenant membership; no client tenant ID is accepted. | Same-tenant success, cross-tenant denial, unassign, role checks. |
| Add incident state transitions | Alert service/endpoints, transition tests, status badge | Legacy `open/acknowledged/resolved` behavior remains. New state is a separate incident field to protect strict consumers. | Transition matrix tests and legacy regression tests. |
| Add investigation notes | Audit writer, safe timeline projection, note endpoint/UI | Notes are bounded, append-only, tenant-scoped, and never returned as arbitrary JSON. | Length/redaction/tenant/timeline ordering tests. |
| Add resolution and false-positive metadata | Nullable alert columns, migration, DTOs, validation | Existing resolved rows remain valid; new final actions require summaries/reasons. | Legacy row compatibility and required-field tests. |
| Add timeline projection | Audit query/service, alert detail response | Uses existing append-only audit integrity; safe allowlisted fields only. | Timeline ordering/filter/redaction tests. |
| Add operational overview wording/links | Existing overview/alerts components and docs | No data fabrication or scoring change. | Frontend build and browser smoke where environment permits. |
| Correct contract/client drift touched by workflow | API docs/schema, `alertApi.ts`, related DTOs | Preserve bookmarked routes and old action endpoints; align query names/cursor behavior. | API contract tests and TypeScript checks. |
| Add SQL migration | `db/migrations/004_alert_incident_workflow.sql`, EF model | Additive only; forward rollback is backup/restore or corrective migration, not destructive drop. | SQL Server execution when available; SQLite persistence tests for model behavior. |
| Update operations documentation | `README.md`, `docs/operations.md`, handoff | Documentation only; no runtime behavior removed. | Command review and clean-clone instructions. |

## Smallest implementation plan

### Phase 1 — GPT-6 Astra: backend and contract foundation

1. Freeze the incident decision in `docs/adr/0006-alerts-as-incidents.md`.
2. Extend the existing `Alert` persistence model with nullable owner and resolution metadata, plus a safe optimistic-concurrency boundary if the current provider supports it without changing legacy rows.
3. Add one additive SQL migration after `003`; never edit `001` or replace the database.
4. Centralize the incident transition policy and validation in the existing alert service.
5. Validate owner subjects through the existing tenant membership store and authenticated `TenantContext`.
6. Generalize the existing audit writer minimally and add alert creation/assignment/note/state timeline events.
7. Add safe alert timeline and owner/member DTOs/endpoints while preserving legacy alert endpoints and wire fields.
8. Add focused service, contract, persistence-isolation, redaction, transition, and compatibility tests.

### Phase 2 — GPT-6.1 Sol: frontend and integration

1. Extend the existing `/app/alerts` inbox and detail drawer; do not create a parallel incident page.
2. Add owner display/assignment, incident state labels, bounded note entry, timeline, resolution summary, and false-positive reason using existing controls and tokens.
3. Preserve evidence, severity, campaign links, keyboard operation, focus trapping/restoration, and loading/empty/error states.
4. Reframe the existing overview as an operations overview and link priority alerts into the existing incident drawer.
5. Align alert query parameters and shared authenticated API behavior with the actual backend contract.
6. Add browser/contract coverage for the new workflow where an authenticated environment is available.
7. Update README, operations, UI/API contracts, and the Phase 2 handoff with actual evidence.

## Explicit non-goals

- No new repository, frontend rewrite, database replacement, or framework migration.
- No separate `Incidents`, `Signals`, workspace hierarchy, queue, cache, or microservice.
- No automatic campaign/bid/spend mutation.
- No new provider connector without credentials and an approved contract.
- No change to scoring formulas, fingerprint semantics, monetary calculations, or tenant authorization.

## Risks and rollback

- SQL Server-specific migration constraints/triggers are not runnable in the current environment. The migration is additive and must be applied and verified against SQL Server before production use.
- Existing `resolved` alerts may lack new resolution metadata; columns remain nullable and requirements are enforced only on new transitions.
- The existing generic audit payload is intentionally not returned. Timeline APIs must use an allowlisted projection to prevent raw data leakage.
- If multi-alert grouping becomes a real requirement, revisit the decision with a new ADR and relationship migration; do not infer it from this slice.
- Rollback is a forward corrective migration or database restore after backup. Dropping incident columns would destroy operational history and is not an acceptable rollback.
