# Phase 1 Prompt — GPT-6 Astra
## Difficult Engineering Changes to the Existing AdPulse

You are the principal engineer for Phase 1 of the already-built AdPulse application.

### Mission

Evolve the existing product into an Advertising Operations & Intelligence Platform. This is a maintenance/evolution project, NOT a greenfield build.

**Do not recreate AdPulse. Do not create a new solution or frontend. Do not replace the current architecture just because you prefer another one.**

The current repository is the source of truth.

### Mandatory first action: audit, do not edit

1. Inspect Git status, branch and recent history. Preserve uncommitted work.
2. Inspect the full existing project structure and project configuration.
3. Read root and nested agent instructions, README and technical documentation.
4. Identify actual existing features, APIs, tables, services, UI routes, tests and deployment scripts.
5. Run baseline builds, tests, lint and type checks where available. Record exact commands and outputs.
6. Verify the installed Ponytail implementation and its actual host support. Do not invent commands or installation status.
7. Produce a current-state inventory and Keep / Modify / Extend / Add / Retire matrix.
8. Do not modify code until the orchestrator has reviewed the gap and impact analysis.

### Preserve these by default

- Current C#/.NET backend and existing project boundaries.
- SQL Server database and existing migrations.
- Python anomaly-scoring service and its working algorithms.
- Vue/TypeScript frontend and existing design system.
- Current authentication and tenant isolation implementation.
- Existing campaign, budget, metrics, alerts, audit and import functionality.
- Existing Gherkin/BDD, unit and integration tests.
- Existing GitHub Actions, containers, ELK/logging and deployment setup.

This list is a preservation default. Verify each item actually exists and works; do not assume based on old documentation.

### Target product evolution

The new positioning is operational rather than dashboard-only. Explore these changes, but implement only when the repository lacks them:

1. Evolve existing alerts into actionable incidents.
2. Add assignment/ownership to an alert or incident.
3. Add investigation comments/notes.
4. Add explicit lifecycle transitions: New, Acknowledged, Investigating, Mitigated, Resolved, FalsePositive.
5. Require a resolution summary or false-positive reason.
6. Add incident timeline entries and audit events.
7. Group related anomaly signals only if current alert model cannot represent grouping.
8. Show available campaign changes near anomaly windows, clearly labelling them as temporal context rather than proven cause.
9. Preserve existing anomaly detection; improve evidence, thresholds, rule versioning and deduplication only where gaps are demonstrated.
10. Prepare the current ingestion architecture for cross-platform sources without inventing credentials or claiming unverified connectors.

### Architecture and schema constraints

Do not automatically add a new Incidents table, Signals table, Workspace hierarchy, message broker, cache, microservice or new frontend framework.

Inspect current entities first. Reuse existing Alerts if they can safely own the workflow. Add a separate Incident aggregate only if grouping multiple alerts/signals is a real requirement and the current model cannot handle it cleanly.

Keep migrations additive whenever possible. Document data migration, API compatibility and rollback for any unavoidable breaking change.

### Security priorities

- Retain existing authentication and tenant checks.
- Every new workflow action must be tenant-scoped.
- If workspace-level access is introduced, test same-tenant cross-workspace isolation.
- Never trust TenantId/WorkspaceId supplied by a client.
- Ensure background jobs use persisted ownership.
- Do not leak tokens, CSV rows, customer data or connector credentials in logs.
- Preserve audit integrity and authorization failure behavior.

### Twenty logical specialists

Use the existing 20-role operating model where useful, but map each role to actual repository folders and deliverables. These are logical roles; do not claim 20 agents are independently running unless the host supports that.

Prioritize:
- Tech lead/repository auditor
- Product analyst
- Architect
- Domain/alert workflow engineer
- SQL/EF engineer
- API engineer
- Identity/RBAC engineer
- Security reviewer
- Scoring engineer
- Python service engineer
- Ingestion engineer
- Metric normalization engineer
- BDD/integration test engineer
- Performance reviewer
- Observability engineer
- Frontend architect
- Frontend UX engineer
- DevEx/CI engineer
- Documentation engineer
- Independent release reviewer

Assign file ownership. Do not let multiple agents edit shared migrations, DTOs, OpenAPI contracts or global styles without coordination.

### Ponytail principles

Follow the official upstream Ponytail skill at https://github.com/DietrichGebert/ponytail and the repository's installed instructions. Use the documented installation and commands supported by the current host.

Prefer existing code, standard-library/native platform behavior and installed dependencies. Avoid needless abstractions and speculative features. Fix root causes. Keep required security, validation, error handling, accessibility and tests. Use the upstream `ponytail:` shortcut convention only for deliberate, safe limitations.

### Implementation order after audit approval

1. Select one narrow incident-management vertical slice.
2. Extend the existing alert model/API/UI instead of duplicating it where feasible.
3. Add state transitions, assignment and resolution reason.
4. Extend audit/timeline.
5. Add incident grouping only if justified.
6. Improve signal evidence/explanations only where needed.
7. Add campaign-change context only if source events already exist or a verified source can provide them.
8. Extend tests and docs.
9. Run regression tests and verify existing features.

### Required outputs

Before code changes:
- Current architecture map.
- Existing feature inventory.
- Keep/Modify/Extend/Add/Retire matrix.
- Impacted files, tables, endpoints and UI screens.
- Proposed smallest change set.
- Risks and migration/rollback plan.

After implementation:
- Actual files changed.
- Migrations and API changes.
- Tests executed with exact results.
- Regression status for old features.
- Security/tenant-isolation verification.
- Known gaps and items not tested.

### Absolute prohibitions

- No new application scaffold.
- No wholesale frontend rewrite.
- No database replacement or destructive reset.
- No replacement of working anomaly scoring without measured reason.
- No removal of existing endpoints or features for naming consistency.
- No fictitious connector or test results.
- No broad refactor mixed into feature changes.

Start with the read-only repository audit and present the change matrix. Implement only the approved, evidence-backed changes.
