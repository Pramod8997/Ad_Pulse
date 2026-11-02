# Phase 2 release-gate test matrix

This matrix is the fast, no-infrastructure release gate for Phase 2 changes. It
uses the frozen Phase 1 contracts and runs without SQL Server, Docker, an OIDC
provider, or a browser. The executable gate is:

```powershell
.\tests\AdPulse.Api.Tests\ReleaseGates\run-release-gates.ps1
```

The script builds the API test project and runs only tests in the
`AdPulse.Api.Tests.ReleaseGates` namespace. It reports environment-dependent
checks as `SKIP [environment]`; a skip is not a pass for that environment.

## Local release gates

| Gate | Executable check | Pass criteria | Environment |
|---|---|---|---|
| Authentication negative cases | `AuthenticationReleaseGateTests` | Missing, malformed, unsigned, invalid-signature, wrong issuer/audience, expired, and no-expiry tokens are rejected; unsafe authority settings are rejected. | Local only |
| Tenant isolation | `TenantIsolationReleaseGateTests` | Tenant claims require server membership; tenant-owned EF entities have query filters; tenant-aware keys and cross-tenant write protection are present. | Local model inspection; no database connection |
| Decimal and null semantics | `FinancialSemanticsReleaseGateTests` | KPI ratios use decimal arithmetic, zero denominators return `null`, omitted budget pacing remains `null`, and money properties retain `decimal(19,4)`. | Local only |
| Idempotency boundaries | `IdempotencyReleaseGateTests` | Daily metrics use a tenant/campaign/date key and alerts use a tenant/fingerprint uniqueness boundary in both EF metadata and the SQL migration. | Local metadata/static migration inspection |
| Scoring contract | `ScoringContractReleaseGateTests` | The schema keeps canonical decimal strings and closed properties; every shared fixture matches the .NET scorer output. | Local only |
| Frontend/API contract presence | `ApiFrontendContractReleaseGateTests` | Accepted API and UI contract files exist and retain their route, tenant, token, null-value, and error-handling declarations. | Local/static files only |

## Environment-dependent checks

These checks are intentionally outside the fast gate and must be run when the
environment is provisioned:

| Check | Current status | Required evidence |
|---|---|---|
| SQL Server migration execution and SQL-specific constraints/triggers | `SKIP [environment]` in the local script; Phase 1 recorded no SQL Server, LocalDB, `sqlcmd`, or Docker. | Apply `db/migrations/001_initial.sql` to SQL Server and exercise composite foreign keys, unique idempotency keys, and the append-only audit trigger. |
| External OIDC discovery, signing-key rotation, and provider-bound bearer flow | `SKIP [environment]`; Phase 1 has no provider configuration. | Run against the configured issuer and verify metadata, issuer, audience, lifetime, signature, and key rotation behavior. |
| Protected workflow HTTP tests | Deferred until Phase 2 workflow routes are mapped. | Exercise API campaign, metric, import, alert, audit, and settings workflows with two tenant memberships. |
| Browser/UI smoke and accessibility checks | Deferred until the Vue shell and screens exist. | Run browser/BDD smoke against the route map, keyboard behavior, loading/empty/error/partial/stale states, and API-backed mutations. |

## Interpretation

- A local gate failure blocks the change and should be fixed before merge.
- An environment skip records missing evidence; it must not be converted to a
  successful release claim.
- The static contract checks protect the Phase 1 freeze. If an accepted
  contract changes, update its consumers, tests, and an ADR before changing
  this matrix.
