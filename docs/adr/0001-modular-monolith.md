# ADR-0001: Modular monolith boundaries

Status: Accepted — Phase 1

## Decision

AdPulse uses one ASP.NET Core API and one SQL Server schema. Domain calculations and tenant-resolution rules live in shared .NET libraries; EF Core and persistence mappings are isolated in Infrastructure. The Python service is a separate stateless scoring boundary that accepts only a versioned scoring contract. The browser is a client and never an authorization boundary.

SQL Server is the source of truth for tenant membership, campaigns, metrics, imports, alerts and audit records. The scoring service does not authorize a caller or persist alert lifecycle state. The ASP.NET API remains the authorization boundary for product workflows.

## Phase 1 evidence and limits

- The ASP.NET host currently maps only anonymous `GET /health`; workflow routing, EF registration and scorer orchestration are not present yet.
- The Python service currently exposes `GET /health` and internal `POST /v1/score`.
- The SQL Server migration is the deployment schema. EF tests use SQLite for provider-independent tenant and change-tracking checks; that test provider does not reproduce SQL Server-only behavior such as the audit trigger.

Kafka, Redis, Kubernetes, event sourcing and additional services are deferred until a measured workload requires them. Product workflow endpoints, persistence orchestration and background jobs are Phase 2 work and are not part of this decision.
