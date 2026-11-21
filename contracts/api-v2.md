# AdPulse API contract v2

Status: frozen Phase 2 DTO and query contract.

This contract defines the workflow boundary only. ASP.NET remains the sole
authorization boundary; the Python scorer consumes `scoring-v1` data and never
selects a tenant or grants access.

## Wire rules

- Workflow JSON uses camelCase names and `application/json`. The scoring
  endpoint retains the `scoring-v1` snake_case contract.
- Dates are ISO-8601 calendar dates (`YYYY-MM-DD`) and represent the tenant's
  reporting-calendar date. A metric ingestion service must derive an event's
  date using the saved IANA reporting timezone; it must reject a missing or
  invalid timezone rather than use the machine timezone.
- Response timestamps are UTC ISO-8601 `date-time` values ending in `Z`.
- Money and ratios use JSON numbers backed by .NET `decimal`; money is
  non-negative and has at most four fractional places, matching SQL Server
  `decimal(19,4)`. Currency is exactly three uppercase ISO-4217 letters.
- Missing metrics and ratios are `null`. Zero is a real value. A ratio whose
  denominator is zero is `null`, never `0`, Infinity or NaN.
- Request bodies never contain an authorization `tenantId`. Tenant scope comes
  from the validated `sub` and `tenant_id` claims plus server-side membership.
  Route IDs, query values and headers cannot override that scope.
- Errors use `application/problem+json`. `detail` is safe for an end user;
  SQL, stack traces, tokens, secrets and uploaded row contents are not
  returned. `correlationId` is returned on every workflow error.

## Authorization and routes

| Route | Access | Purpose |
|---|---|---|
| `GET /app` | tenant member | Overview for one reporting range and one currency scope |
| `GET /app/campaigns` | tenant member | Stable cursor-paged campaign list |
| `POST /app/campaigns` | tenant member | Create a campaign |
| `GET /app/campaigns/{campaignId}` | tenant member | Campaign detail |
| `PATCH /app/campaigns/{campaignId}` | tenant member | Edit a campaign |
| `GET /app/campaigns/{campaignId}/metrics` | tenant member | Stable cursor-paged daily metrics |
| `PUT /app/campaigns/{campaignId}/metrics/{metricDate}` | tenant member | Create or replace one daily metric key |
| `GET /app/imports` | import permission | Import history |
| `POST /app/imports` | import permission | Upload and validate a CSV (`file` multipart field) |
| `GET /app/imports/{importId}` | import permission | Import result and bounded row errors |
| `GET /app/alerts` | tenant member | Stable cursor-paged alerts and incident ownership/state |
| `GET /app/alerts/{alertId}` | tenant member | Alert evidence and incident detail/timeline |
| `PATCH /app/alerts/{alertId}` | tenant member | Acknowledge, resolve or reopen an alert |
| `PATCH /app/alerts/{alertId}/incident` | tenant member | Advance the incident state with confirmation and required final summary/reason |
| `PATCH /app/alerts/{alertId}/assignment` | tenant member | Assign or unassign a same-tenant owner |
| `POST /app/alerts/{alertId}/notes` | tenant member | Append a bounded investigation note |
| `GET /app/alerts/{alertId}/timeline` | tenant member | Read the safe, tenant-scoped incident timeline |
| `GET /app/audit` | audit permission | Read-only stable cursor-paged audit log |
| `GET /app/settings` | tenant admin | Tenant reporting settings |
| `PATCH /app/settings` | tenant admin | Update tenant settings |

The server applies tenant query filters before route/entity lookup. A resource
from another tenant is indistinguishable from a missing resource (`404`), and
authorization failures do not reveal whether another tenant owns an ID.

## Pagination and query semantics

List responses use:

```json
{
  "items": [],
  "nextCursor": "opaque-server-value-or-null",
  "hasMore": false,
  "limit": 25
}
```

`limit` is an integer from 1 through 100, defaults to 25, and is applied after
tenant and other filters. `cursor` is opaque, must be returned unchanged by
the client, and is rejected when it is malformed, expired or incompatible with
the current filter/sort. Offset pagination is not supported.

The server rejects `limit` values outside 1 through 100; it does not silently
clamp an invalid request.

Every ordering has a unique ID tie-breaker and is applied before taking the
page:

- campaigns: `createdAt DESC, id DESC` by default; optional `name`, `status`,
  `budget` or `createdAt`, each with `id` as the final tie-breaker;
- metrics: `metricDate DESC` for one campaign (the key is unique);
- imports: `uploadedAt DESC, id DESC`;
- alerts: `detectedAt DESC, id DESC`;
- audit: `occurredAt DESC, id DESC`.

Query dates are inclusive. `from` must not be after `to`. Campaign and overview
aggregation never combines currencies: a caller supplies `currency` when the
tenant has more than one campaign currency, and an ambiguous request returns
`422` with code `currency_scope_required`.

## Shared DTOs

`ProblemDetailsDto` is the RFC 7807 shape plus `code`, `correlationId` and an
optional field-error map. `CursorPage<T>` is the page shape above.
`KpiMetricsDto` contains nullable `ctr`, `cpc`, `cpa`, `roas` and
`budgetPacing`; all ratios use the Phase 1 formulas.

## Campaign and metric DTOs

Campaign write requests contain only `name`, `status`, `currency`, `budget`,
`startsOn` and `endsOn`; the server owns the ID, tenant and `createdAt`. Status
is one of `draft`, `active`, `paused` or `completed`. A campaign's end date is
inclusive and cannot precede its start date.

`DailyMetricDto` contains the persisted daily key, non-negative spend, revenue,
impressions, clicks and conversions, plus nullable KPI values calculated with
the Phase 1 formulas. Clicks cannot exceed impressions and conversions cannot
exceed clicks. `PUT` replaces the complete
value for `(authorized tenant, campaignId, metricDate)`, so a retry does not
create a second daily row. A campaign's currency is the currency of its
metrics; cross-currency writes are rejected.

## Import DTOs

The upload is multipart and has one `file` field. The API reports filename,
status, accepted/rejected counts and bounded row errors containing only
`rowNumber`, `errorCode` and a safe `message`; it never returns the uploaded
file body or raw rows. Import state is `validated`, `completed`, `rejected` or
`conflict`.

## Alert DTOs

Alerts expose the `scoring-v1` result fields, evidence window and legacy lifecycle
status (`open`, `acknowledged` or `resolved`). They also expose an additive
incident projection with `incidentStatus` (`new`, `acknowledged`,
`investigating`, `mitigated`, `resolved` or `false_positive`), optional owner,
assignment time, resolution metadata and a bounded timeline. `confidence` is
data sufficiency, not a probability. Fingerprints are lowercase 64-character
SHA-256 values and are unique within a tenant. Existing lifecycle requests and
routes remain valid; the incident route requires confirmation, a resolution
summary when resolving, and a reason when marking a false positive. Notes are
append-only and capped at 2,000 characters. The server owns alert IDs, tenant
scope, membership validation and audit writes. Raw audit payload JSON is never
returned.

## Audit, overview and settings DTOs

Audit is read-only from the browser. Its response exposes actor, action,
entity type, entity ID and UTC occurrence time; arbitrary event creation is not
an API operation. Overview includes the selected reporting timezone, date range,
single currency, nullable KPI values, daily spend/revenue, budget pacing and a
bounded priority-alert list. Settings exposes the IANA reporting timezone,
reporting currency and UTC update time; only a tenant admin may read or update
it. The tenant identity is never a settings field.
