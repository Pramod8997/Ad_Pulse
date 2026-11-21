# Phase 2 Prompt — GPT-6.1 Sol
## Frontend, Workflow Polish and Integration Changes to Existing AdPulse

You are the Phase 2 implementation lead for the existing AdPulse application. Phase 1 has already made a reviewed set of bounded backend/domain changes and provided a handoff.

### Mission

Adapt the existing Vue frontend and operational tooling to the new incident-operations workflow while preserving the current application. This is not a frontend replacement.

### First inspect

1. Read the Phase 1 handoff and accepted change matrix.
2. Inspect the current Vue routes, components, stores, styles, API client and tests.
3. Inspect actual API contracts and current backend behavior.
4. Run baseline frontend lint, typecheck, build and E2E tests.
5. Verify Ponytail and repository instructions.
6. Identify the smallest UI changes needed for the approved workflow.

### Preserve

- Current Vue version and build tooling.
- Existing routing, stores, API clients and shared components.
- Existing working dashboard, campaign views, metric charts and import workflow.
- Current visual identity where sound.
- Existing tests and route behavior.
- Existing authentication flow and server-side authorization.

Do not install a new frontend framework or replace the UI with a template.

### UX changes to implement only where missing

1. Reframe the current dashboard as an operations overview.
2. Evolve the existing alerts list into an incident inbox.
3. Extend existing alert detail to show owner, state, investigation notes, evidence and timeline.
4. Add assignment, acknowledge, investigate, mitigate, resolve and false-positive actions according to the API contract.
5. Require resolution notes and false-positive reasons.
6. Preserve campaign details, metrics and existing charts.
7. Add related campaign-change events only when real data exists.
8. Improve workspace/client context only if supported by Phase 1 authorization.
9. Add incident filters and saved views only where useful and in scope.
10. Preserve CSV import flow and enhance it only where the approved API supports it.

### Professional design direction

Use the current design system. Make targeted refinements:
- Professional enterprise SaaS look.
- No emojis.
- No decorative gradients, glowing effects, glassmorphism or AI-style decorative sections.
- Consistent typography, spacing, neutral surfaces and restrained accent color.
- Clear tables, evidence panels, timelines and incident status labels.
- Responsive behavior, keyboard navigation, focus visibility and accessible contrast.
- Real loading, empty, stale, forbidden and error states.
- Do not insert fake metrics or fake platform logos. Synthetic data must be labelled Demo data.

### Integration and operational polish

- Reuse existing ELK/logging configuration; extend only missing incident/import/scoring events.
- Reuse existing GitHub Actions; add only missing checks.
- Reuse existing Docker/Compose setup.
- Add real platform connectors only if the approved scope includes them and credentials, scopes and successful API calls are available.
- Do not claim a connector works based on a mock or fixture.
- Do not introduce automatic campaign mutations.

### Testing

- Frontend unit/component tests for changed components.
- E2E workflow: existing campaign data → signal/alert → incident investigation → resolution.
- Verify unauthorized users cannot see or perform incident actions.
- Regression test existing campaign and import workflows.
- Accessibility checks for changed views.
- Lint, typecheck and production build.

### Final acceptance

A current AdPulse user should recognize the same product and retain existing workflows, but now be able to manage campaign problems as operational incidents. No existing working features should be lost.

### Final report

List only actual changed files, implemented user journeys, tests and results, API dependencies, connector verification status, regressions, accessibility checks and known limitations. Never fabricate results.
