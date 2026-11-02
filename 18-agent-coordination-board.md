# 18 — Agent Coordination Board Template

Copy this table into GitHub Projects, Linear, Jira or the coding host's task tracker. Keep one source of truth.

| Task | Phase | Primary agent | Reviewer | Files / contract | Depends on | Status | Evidence |
|---|---|---|---|---|---|---|---|
| AP-001 Reconnaissance | 1 | 01 | 20 | Existing repo | None | Not started | Baseline commands |
| AP-002 Product/ADR freeze | 1 | 02/03 | 01/20 | docs/ADR | AP-001 | Not started | ADR links |
| AP-003 Tenant identity | 1 | 07 | 08 | Auth boundary | AP-002 | Not started | Integration tests |
| AP-004 Tenant-aware schema | 1 | 05 | 08 | EF migrations | AP-002 | Not started | Migration + DB tests |
| AP-005 API/scoring contracts | 1 | 03/06/10/11 | 01 | OpenAPI + Pydantic | AP-002 | Not started | Contract tests |
| AP-006 KPI/score engine | 1 | 10 | 14/20 | Domain + scorer | AP-005 | Not started | Golden fixtures |
| AP-007 P0 security/BDD tests | 1 | 08/14 | 20 | Tests | AP-003..006 | Not started | Test output |
| AP-008 Phase 1 handoff | 1 | 01/19/20 | All | phase-1-handoff.md | AP-003..007 | Not started | Commit SHA |
| AP-009 Campaign/metric workflows | 2 | 06/09 | 14 | API contract | AP-008 | Not started | Integration tests |
| AP-010 Alert lifecycle | 2 | 06/10/11 | 14 | Scoring contract | AP-008 | Not started | Contract/integration |
| AP-011 Vue app shell/pages | 2 | 12/13 | 20 | UI spec | AP-008 | Not started | Running app/screens |
| AP-012 BDD/E2E completion | 2 | 14 | 08/20 | Test matrix | AP-009..011 | Not started | Test output |
| AP-013 Observability/local stack | 2 | 16/17 | 20 | Compose/log schema | AP-008 | Not started | Kibana evidence |
| AP-014 CI/release | 2 | 18/19/20 | 01 | GitHub Actions | AP-009..013 | Not started | Green CI run |

Statuses: Not started / Ready / In progress / Blocked / In review / Verified / Deferred. Every Blocked/Deferred row needs an owner, reason and revisit condition.

## Concurrency policy
- At most one writer for shared API contracts, database migrations, global CSS tokens and package manifests.
- Implementers can parallelize by bounded feature branch after the contract is frozen.
- Independent reviewer never approves its own change.
- Integrator merges in dependency order and runs the full suite after each milestone.
- If actual agent count is lower than 20, keep all role responsibilities but record the actual runtime concurrency.
