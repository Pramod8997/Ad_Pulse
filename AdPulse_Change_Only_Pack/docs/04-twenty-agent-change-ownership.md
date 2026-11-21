# 04 — Twenty-Agent Change Ownership

The 20 agents must work against the existing repository, not a newly generated architecture.

| # | Role | Main responsibility | Change rule |
|---|---|---|---|
| 1 | Orchestrator / Tech Lead | Current-state audit, task graph, approvals and integration | Owns change matrix and merge decisions |
| 2 | Product Analyst | Compare current product with target workflows | Must identify existing functionality before proposing additions |
| 3 | Solution Architect | Impact analysis and ADRs | Preserve architecture unless evidence requires change |
| 4 | Domain Engineer | Alert/incident model and state machine | Extend current entities where suitable |
| 5 | SQL/EF Engineer | Existing schema and migrations | Additive migrations; no reset |
| 6 | API Engineer | Existing endpoints and DTOs | Backward compatibility |
| 7 | Identity/RBAC Engineer | Existing authentication and permission policies | Never weaken access checks |
| 8 | Security Reviewer | Tenant/workspace tests | Negative tests are release-blocking |
| 9 | Scoring Engineer | Existing anomaly rules and explanations | Preserve working algorithm; targeted improvements only |
| 10 | Python Engineer | Existing scoring service | Keep existing contract compatible unless approved |
| 11 | Ingestion Engineer | Current CSV/import paths | Reuse and extend |
| 12 | Data/Normalization Engineer | Metric definitions and source provenance | Never silently change KPI semantics |
| 13 | Incident Workflow Engineer | Assignment, notes, lifecycle and grouping | Avoid duplicate workflow model |
| 14 | BDD/Test Engineer | Regression and new workflow tests | Existing tests remain active |
| 15 | Performance Engineer | Query and UI performance | Measure before optimizing |
| 16 | Frontend Architect | Existing Vue architecture | No rewrite or framework migration |
| 17 | Frontend UX Engineer | Adapt current dashboard/alerts | Preserve current visual system |
| 18 | Observability Engineer | Existing logs/ELK | Add only missing events/dashboards |
| 19 | CI/DevEx Engineer | Existing pipelines and local environment | Extend existing configuration |
| 20 | Independent Reviewer | Ponytail review, regression and release audit | Reject unnecessary rewrites |

## Work protocol
Each agent receives:
- Existing files/modules it owns.
- Existing behavior that must remain.
- Specific change objective.
- Acceptance criteria.
- Tests to run.
- Explicit non-goals.

The orchestrator must not assign overlapping write ownership without a coordination plan. All shared contract/schema changes require one owner and review.
