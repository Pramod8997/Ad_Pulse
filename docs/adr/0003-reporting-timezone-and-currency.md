# ADR-0003: Reporting date, timezone and currency policy

Status: Accepted — Phase 1

## Reporting dates and timestamps

Daily metrics are keyed by `MetricDate`, a calendar date in the tenant's reporting timezone. The first ingestion workflow must convert an event timestamp with that tenant policy before persisting the date, and must reject a missing or invalid policy instead of using the machine timezone. Audit and operational timestamps use `DateTimeOffset`; an API boundary that exposes them must use UTC ISO-8601 values.

Phase 1 has no tenant timezone column, timezone settings endpoint or ingestion workflow. The current model only persists the caller-provided `DateOnly` metric date, and the current code does not perform timezone conversion or normalize offsets. Those implementation gaps are explicit Phase 2 prerequisites, not completed behavior.

## Currency and decimal arithmetic

Each campaign has one ISO-4217 currency and any future cross-currency aggregation must be rejected. No aggregation workflow exists in Phase 1. The current database stores `Currency` as `char(3)` and checks only three uppercase ASCII letters; it does not contain an ISO-4217 reference table. Campaign budgets and metric spend/revenue use SQL Server `decimal(19,4)` and .NET `decimal`.

The implemented KPI formulas are CTR = clicks / impressions, CPC = spend / clicks, CPA = spend / conversions, ROAS = revenue / spend and budget pacing = spend / planned-to-date budget. A zero denominator returns `null`, never Infinity or NaN. `KpiCalculator` does not round its results; presentation/export rounding is deferred and, when implemented, uses decimal half-up semantics.
