# 18 — Agent Coordination Board Template

Copy this table into GitHub Projects, Linear, Jira or the coding host's task tracker. Keep one source of truth.

| Task | Phase | Primary agent | Reviewer | Files / contract | Depends on | Status | Evidence |
|---|---|---|---|---|---|---|---|
| AP-001 Reconnaissance | 1 | 01 | 20 | Existing repo | None | Verified | Toolchain and baseline recorded in `docs/phase-1-handoff.md` |
| AP-002 Product/ADR freeze | 1 | 02/03 | 01/20 | docs/ADR | AP-001 | Verified | ADR-0001 through ADR-0005 |
| AP-003 Tenant identity | 1 | 07 | 08 | Auth boundary | AP-002 | Verified | Identity and tenant negative tests |
| AP-004 Tenant-aware schema | 1 | 05 | 08 | SQL + EF model | AP-002 | Verified | `001_initial.sql`, 13 isolation tests; SQL Server execution blocked |
| AP-005 API/scoring contracts | 1 | 03/06/10/11 | 01 | DTO + Pydantic | AP-002 | Verified | `api-v1`, `scoring-v1`, 13 Python tests |
| AP-006 KPI/score engine | 1 | 10 | 14/20 | Domain + scorer | AP-005 | Verified | 4 shared fixtures, .NET scoring/KPI tests |
| AP-007 P0 security/BDD tests | 1 | 08/14 | 20 | Tests | AP-003..006 | Verified | 47 .NET tests; BDD deferred to Phase 2 |
| AP-008 Phase 1 handoff | 1 | 01/19/20 | All | phase-1-handoff.md | AP-003..007 | Verified | `475a72bc67dbf9cfbcfac2126afa8423aa7099f7` |
| AP-009 Campaign/metric workflows | 2 | 06/09 | 14 | API contract | AP-008 | Deferred | Phase 2 owner 06/09; starts after handoff verification |
| AP-010 Alert lifecycle | 2 | 06/10/11 | 14 | Scoring contract | AP-008 | Deferred | Phase 2 owner 06/10/11; starts after API workflows |
| AP-011 Vue app shell/pages | 2 | 12/13 | 20 | UI spec | AP-008 | Deferred | Phase 2 owner 12/13; starts after API DTOs freeze |
| AP-012 BDD/E2E completion | 2 | 14 | 08/20 | Test matrix | AP-009..011 | Deferred | Phase 2 owner 14; starts after workflows and UI |
| AP-013 Observability/local stack | 2 | 16/17 | 20 | Compose/log schema | AP-008 | Deferred | Phase 2 owner 16/17; starts after service boundaries |
| AP-014 CI/release | 2 | 18/19/20 | 01 | GitHub Actions | AP-009..013 | Deferred | Phase 2 owner 18/19/20; starts after final checks |

Statuses: Not started / Ready / In progress / Blocked / In review / Verified / Deferred. Every Blocked/Deferred row needs an owner, reason and revisit condition.

## Concurrency policy
- At most one writer for shared API contracts, database migrations, global CSS tokens and package manifests.
- Implementers can parallelize by bounded feature branch after the contract is frozen.
- Independent reviewer never approves its own change.
- Integrator merges in dependency order and runs the full suite after each milestone.
- If actual agent count is lower than 20, keep all role responsibilities but record the actual runtime concurrency. 

Phase 1 runtime concurrency: 10 bounded agents ran in parallel; the remaining logical roles were covered by the integrator or deferred to Phase 2.
