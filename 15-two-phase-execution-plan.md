# 15 — Two-Phase Delivery and Model Handoff

## Intent

Use the strongest reasoning allocation for high-risk architecture and foundations, then use the second requested model allocation for routine feature completion, frontend implementation, integration and cleanup. The model labels are user-requested routing names: **Phase 1: GPT-6 Astra**; **Phase 2: GPT-6.1 Sol**. The execution host must check availability before each phase. These labels are not a promise that every IDE/agent platform exposes those exact model IDs.

## Phase 1 — GPT-6 Astra: hard engineering and stable foundations

### Phase 1 objective

Resolve the areas where a wrong early decision causes expensive rewrites or security exposure. Produce code, tests and signed-off contracts—not merely architecture prose.

### Workstreams
A. Repository reconnaissance: inspect files, current conventions, package/runtime versions, baseline build/tests and existing UI components. Do not overwrite a functioning repo.
B. Architecture and contracts: confirm modular monolith, API DTOs, scoring schema, error semantics, reporting date/timezone, currency policy, idempotency and background job behavior.
C. Identity and tenancy: validated identity provider integration boundary, membership resolution, tenant-scoped EF patterns, composite tenant-aware foreign keys, role matrix.
D. Database foundation: migrations, constraints, indexes, precision, unique daily metric key, alert fingerprint strategy and audit transaction semantics.
E. KPI semantics: safe CTR/CPC/CPA/ROAS formulas, null/zero denominator semantics, budget pacing edge cases and decimal rounding.
F. Scoring engine: explainable deterministic baseline/rule scoring, cold-start behavior, score/severity mapping, confidence semantics and stable fingerprints.
G. Critical verification: cross-tenant integration tests, malformed input tests, idempotency tests, scorer golden fixtures and Gherkin scenarios.
H. Frontend architecture definition: lock route map, API client shape, token strategy and design-system contract. Do not spend Phase 1 on ornamental polish.

### Phase 1 deliverables
- `docs/phase-1-handoff.md` with completed/incomplete status.
- Accepted ADRs and approved API/scoring contract versions.
- Database migration and schema constraints.
- Auth/tenant context foundation with negative integration tests.
- Domain KPI calculator and tested edge cases.
- Scoring engine/service skeleton and deterministic contract tests.
- CI baseline for build, unit tests and critical integration tests.
- UI route/component/token plan aligned with `14-frontend-design-system.md`.
- Known risks and explicit deferred scope.

### Phase 1 exit gate — all mandatory
- Clean build passes for implemented projects.
- Cross-tenant read/write tests pass for campaigns, metrics, imports, alerts and audit paths that exist.
- Auth identity/issuer/audience/expiry behavior is verified in tests or documented as blocked on provider configuration.
- SQL constraints prevent cross-tenant metric-to-campaign associations.
- Monetary arithmetic is decimal-based and division-by-zero returns documented null/undefined.
- Repeated metric ingestion/scoring is idempotent.
- Scoring contract fixtures prove deterministic, explainable results.
- No critical unresolved design conflict remains.
- Handoff records actual commands, outcomes, skipped checks and remaining risks.

Do not start Phase 2 implementation against a moving contract. If a contract must change, create an ADR and update consumers/tests.

## Phase 2 — GPT-6.1 Sol: product completion, frontend, integration and cleanup

### Phase 2 objective

Complete end-user workflows and produce a cohesive, usable, professional application against Phase 1's frozen contracts.

### Workstreams
A. Complete campaign CRUD, metrics API, CSV import and row-level error handling.
B. Finish Python scoring HTTP service, client integration, retry/timeout handling and persisted alert lifecycle.
C. Build the Vue application to the visual specification: responsive shell, overview, campaigns, campaign detail, alerts, imports, audit and settings screens where authorized.
D. Add charts, filters, pagination, loading/empty/error/partial/stale states, accessibility and real mutation feedback.
E. Complete Gherkin/BDD, integration, contract and browser smoke tests.
F. Add structured logs, correlation, ELK local profile and operational runbooks.
G. Complete Docker Compose local setup, GitHub Actions, dependency checks and deployment/rollback instructions.
H. Independent review, `ponytail-review`, `ponytail-audit`, debt ledger, refactor only where justified, remove dead code and update documentation.

### Phase 2 exit gate
- User can complete core workflows end-to-end with seeded demo data.
- API remains the sole authorization boundary; frontend route guards are only UX, never security.
- Tenant isolation suite remains green after every merge.
- UI passes the design/accessibility/responsive checklist.
- No inert controls, fake metrics, unlabelled synthetic data or dead-end navigation.
- CI runs build, lint/typecheck, unit tests, integration/BDD tests and dependency/security checks available to the repo.
- ELK profile can ingest searchable structured logs locally, or its blocker is documented.
- `README.md` supports clean-clone setup and troubleshooting.
- Independent reviewer signs off or lists release-blocking issues.

## Contract freeze and handoff protocol

At Phase 1 completion, publish a single `phase-1-handoff.md` containing:
1. Git commit SHA.
2. Runtime/toolchain versions and commands run.
3. Accepted database schema/migration version.
4. API OpenAPI/DTO contract version.
5. Python scoring request/response schema version.
6. Metric formula and timezone/currency policy.
7. Tenant authorization design and all tested negative cases.
8. Test output summaries with actual pass/fail counts.
9. Known issues, shortcuts and migration risks.
10. Phase 2 task list and exact dependency order.

Phase 2 starts by reading this file and checking the referenced commit. It must not reinterpret Phase 1 assumptions silently.

## Agent synchronization

Create one coordinator and five squads of four agents. The coordinator owns the task graph, shared contracts and integration queue. Each task has a single primary owner and a reviewer. Agents may work in parallel only when file ownership and dependencies do not overlap. Shared files (`*.sln`, project files, package manifests, EF migrations, OpenAPI schema, global tokens) are serialized through the integrator.

## Failure policy

If a model, subagent, external service, test database or deployment credential is unavailable, record the blocker and proceed with independent work that is safe. Never claim an action occurred without evidence. Do not create a fake `phase-1-handoff.md` with imagined passing tests.
