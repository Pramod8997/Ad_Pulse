# ADR-0005: Database idempotency and audit semantics

Status: Accepted — Phase 1

## Duplicate and tenant boundaries

`DailyMetrics` has a composite primary key `(TenantId, CampaignId, MetricDate)`, so SQL Server cannot store two rows for the same tenant, campaign and reporting date. In the current EF tests, a repeated `Add` reaches the unique-key violation and raises `DbUpdateException`; it is a duplicate-rejection guard, not an implemented successful no-op or upsert. The Phase 1 repository has no metric-ingestion handler that translates a retry into an idempotent response.

The scoring functions are deterministic for the same request. `Alerts` has a unique `(TenantId, Fingerprint)` constraint, so a repeated alert insert is rejected rather than creating a second row. No Phase 1 scorer-orchestration or alert-lifecycle handler currently converts that conflict into a successful retry response.

All tenant-owned foreign keys include `TenantId`: memberships, campaigns, daily metrics, import batches, import row errors, alerts and audit events cannot reference a parent from another tenant. The EF model also repeats the tenant key in these relationships, while global query filters and `SaveChanges` tenant checks protect the application boundary.

## Audit and import semantics

Audit events are append-only. `AdPulseDbContext` rejects modified or deleted `AuditEvent` entries before saving, and the SQL Server migration adds `TR_AuditEvents_AppendOnly` as a second boundary. The migration also requires valid JSON payloads. The SQLite-based EF tests verify the application guard; they do not execute the SQL Server trigger.

Import row errors store bounded metadata (`ErrorCode` and a message capped at 500 characters) and never store raw uploaded row contents. The import batch retains filename and row counts, not the uploaded file body.
