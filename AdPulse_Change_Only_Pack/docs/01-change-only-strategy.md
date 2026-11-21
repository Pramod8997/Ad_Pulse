# Change-Only Strategy: Evolve the Existing AdPulse

## 1. Decision

The previous AdPulse application has already been built. The new product positioning does not justify discarding that work.

The existing repository is the source of truth. Preserve its current technology stack, architecture, naming conventions, database, API routes, frontend framework, tests and deployment setup unless a documented gap makes a specific change necessary.

**Target positioning:** Advertising Operations & Intelligence Platform.

**Implementation approach:** incremental evolution, not replacement.

## 2. Product changes are primarily workflow changes

The previous product already monitors campaigns, budgets, daily metrics and performance anomalies, and provides alerts and audit history.

The revised product should build on those capabilities:
- Keep current campaign monitoring and performance metrics.
- Keep the existing anomaly-scoring service and improve it only where the evidence shows a gap.
- Evolve existing alerts into operational incidents where useful.
- Add assignment, investigation notes, controlled state transitions and resolution summaries only where missing.
- Extend audit history to cover new incident actions.
- Adapt the existing dashboard into an operations-oriented experience rather than replacing it wholesale.
- Prepare cross-platform data ingestion through the existing import/integration boundaries. Implement new provider connectors only when explicitly prioritized and credentials/API access are available.

## 3. Repository-first rule

Before planning work, inspect:
- Current Git branch, status and recent commit history.
- Solution/project files and actual folder structure.
- Existing API endpoints, DTOs, services and dependency registrations.
- SQL schema, EF Core models/migrations, indexes and foreign keys.
- Existing tenant model and authorization middleware/policies.
- Python scoring implementation, schemas and tests.
- Vue components, routes, stores, API clients and design tokens.
- Existing Gherkin, unit, integration and E2E tests.
- GitHub Actions, Docker/Compose, logging and deployment.
- Current README, ADRs and product documentation.

Run all currently available build/test/lint commands before changing anything. Record failures as baseline failures; do not attribute pre-existing failures to new work.

## 4. Change classification

Every proposed feature or modification must be classified:

| Classification | Meaning | Rule |
|---|---|---|
| KEEP | Existing implementation already satisfies the target | Do not rewrite it |
| MODIFY | Existing implementation needs a bounded change | Change the smallest affected surface |
| EXTEND | Existing feature needs additive capability | Preserve old behavior and contract |
| ADD | No suitable implementation exists | Add only after proving the gap |
| RETIRE | Existing feature is genuinely obsolete | Deprecate with owner approval and migration plan |

Create a feature inventory with: current implementation, files/modules, tests, status, proposed classification, change rationale, impact, migration needs and acceptance tests.

## 5. Likely product evolution (validate against the repo)

Do not assume anything in this list is missing. Inspect first.

- Existing alerts may already contain severity, evidence and lifecycle status. Reuse and extend them into incidents rather than introducing a parallel incident system unnecessarily.
- Existing audit history should be extended to record assignment, comments, investigation, mitigation, resolution and false-positive decisions.
- Existing campaign and tenant data structures should be retained. Introduce a client-workspace level only if the current customer/tenant model cannot represent agency-to-client authorization safely.
- Existing anomaly detection should remain the source of signals. Improve explainability, rule versioning, deduplication or grouping only where absent.
- Existing Vue dashboard should be adapted using current components and styles. Do not replace it with a fresh template.
- Existing CSV/API ingestion should be reused. Additional connectors are a separate, prioritized extension.
- Existing CI/CD and ELK configuration should be extended, not replaced.

## 6. Backward compatibility

For every change, state:
- Existing behavior affected.
- Existing API request/response compatibility.
- Database migration and rollback implications.
- Frontend route/component impact.
- Existing data migration needs.
- Test coverage needed.

Prefer additive schema and API changes. Do not rename/remove columns or routes just to make the new concept cleaner. If a breaking change is necessary, provide a compatibility window and migration plan.

## 7. Data model guidance

Do not automatically add new tables named Incidents, Workspaces or Signals. First inspect the current schema.

Possible approaches:
- If current Alerts already provide the operational case lifecycle, extend Alerts with assignment, comments, investigation/resolution metadata and timeline events.
- If one alert may group multiple anomaly detections, introduce an Incident aggregate plus a relationship to existing alert/signal records, but only if this is a real domain need.
- If current tenant hierarchy already supports organizations and customers, reuse it. Add a client workspace abstraction only if required for permission boundaries.
- Preserve source data and metric provenance. Do not silently reinterpret CPC/CPA/ROAS or aggregate currencies.

Document the chosen option in a short ADR before schema implementation.

## 8. UI guidance

Reposition existing screens rather than rebuilding the frontend:
- Existing dashboard → operations overview.
- Existing alerts page → incident inbox with filters and ownership.
- Existing alert detail → investigation detail with evidence and timeline.
- Existing campaign pages → retain metrics and add linked incidents/change context.
- Existing import page → retain and add provenance/status only where missing.

Maintain the current design system where sound. Improve consistency, typography, density, responsiveness and accessibility. Professional enterprise visual style; no emojis, gimmicky gradients or artificial AI-template decoration.

## 9. Non-negotiable safety and engineering requirements

- Preserve tenant isolation and authorization.
- Never trust client-supplied tenant/workspace ownership identifiers.
- Do not reduce validation, error handling, idempotency or audit integrity.
- Use decimal for monetary calculations.
- Do not claim temporal correlation proves root cause.
- Do not enable automated ad spend/bid/campaign changes as part of this repositioning.
- Do not fabricate API integrations, model accuracy, performance results or test outcomes.
- Keep secrets and raw imported records out of logs.

## 10. Change control

Before implementation:
1. Produce a current-state map.
2. Produce a Keep/Modify/Extend/Add/Retire matrix.
3. Produce a list of impacted files/modules/tables/endpoints.
4. Identify risks and migration/rollback strategy.
5. Identify existing tests that protect behavior.
6. Obtain orchestrator review of the proposed change set.

Implement one vertical slice at a time. After each slice, run focused tests and relevant regression tests. Keep commits small and reviewable.

## 11. Definition of done

A change is complete only when:
- It closes an identified gap in the existing product.
- The smallest adequate code surface was changed.
- Previous functionality remains intact.
- Relevant tests pass.
- Database/API compatibility is verified.
- Security and tenant boundaries remain intact.
- Documentation reflects actual implementation.
- Unverified work and residual risks are disclosed.
