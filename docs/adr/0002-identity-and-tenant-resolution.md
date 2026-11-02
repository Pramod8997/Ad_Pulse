# ADR-0002: Identity and tenant resolution

Status: Accepted — Phase 1

## Decision

The ASP.NET host validates OIDC signature metadata, issuer, audience and lifetime through the configured bearer authority, with zero clock skew. A tenant workflow must then call `TenantContextResolver`. The resolver requires an authenticated principal with exactly one distinct, non-blank `sub` and `tenant_id` claim. Each value is limited to 128 characters and may not contain the scoring fingerprint separator (`|`). It looks up `(subject, tenant)` in SQL Server and returns the membership's tenant, subject and roles. A missing membership is denied.

Roles come from membership rows and are case-insensitive in the EF store. There is no client-selected tenant header, route fallback or default tenant. A token claim alone never grants membership.

Every tenant-scoped EF context receives a `TenantScope`. Global query filters constrain reads for Tenant, Membership, Campaign, DailyMetric, ImportBatch, ImportRowError, Alert and AuditEvent. `SaveChanges` rejects tracked tenant-owned writes outside the scope. `TenantScope.For` limits the persisted tenant identifier to 64 characters; the system scope is reserved for migrations and controlled maintenance, not web requests. Composite foreign keys repeat `TenantId` so SQL Server also prevents cross-tenant associations.

## Phase 1 boundary

The resolver and scoped EF behavior are implemented and covered by tests, but the current ASP.NET `Program` does not yet register the resolver, create a per-request `TenantScope` or expose a protected workflow route. That wiring is required before Phase 2 product endpoints are enabled; this ADR does not claim it is already live.
