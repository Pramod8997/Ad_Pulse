# Demo data disclosure

## [Demo data]

`db/seed/001_demo.sql` contains synthetic data for local development, demonstrations, and workflow tests. It is not production data, must not be used to represent a customer, campaign, or business result, and must not be applied to a production database.

The seed contains:

- two fictional tenants: `demo-acme` and `demo-northstar`;
- placeholder OIDC subject identifiers beginning with `demo-`;
- tenant memberships with administrator and analyst roles;
- campaigns in USD and EUR, with daily metrics;
- one completed import with one synthetic row error;
- deterministic alerts and append-only audit events.

Every tenant-owned row uses the matching tenant ID, and every campaign association uses the matching tenant/campaign composite key. The data therefore exercises the same tenant boundary and foreign-key shape as application data. It does not grant authorization: real requests still require a validated OIDC identity and server-side membership.

## No secrets or personal data

The seed contains no passwords, access tokens, signing keys, connection strings, credentials, real user identities, uploaded customer rows, or production identifiers. The `demo-*` subject values are inert test labels, not login accounts. Configure real OIDC and database values through the local environment or an approved secret store; never add them to this seed or to committed configuration.

## Applying the seed

Apply the ordered migrations (`001_initial.sql` through `004_alert_incident_workflow.sql`) first, then run the seed against the same database:

```powershell
sqlcmd -S $env:ADPULSE_SQL_SERVER -d $env:ADPULSE_SQL_DATABASE -E -b -i db/migrations/001_initial.sql
sqlcmd -S $env:ADPULSE_SQL_SERVER -d $env:ADPULSE_SQL_DATABASE -E -b -i db/migrations/002_tenant_reporting_settings.sql
sqlcmd -S $env:ADPULSE_SQL_SERVER -d $env:ADPULSE_SQL_DATABASE -E -b -i db/migrations/003_import_pending_metrics.sql
sqlcmd -S $env:ADPULSE_SQL_SERVER -d $env:ADPULSE_SQL_DATABASE -E -b -i db/migrations/004_alert_incident_workflow.sql
sqlcmd -S $env:ADPULSE_SQL_SERVER -d $env:ADPULSE_SQL_DATABASE -E -b -i db/seed/001_demo.sql
```

The seed uses fixed IDs, `IF NOT EXISTS` guards, and one transaction. It is safe to rerun without deleting rows. Existing rows with those IDs are intentionally left unchanged; use a fresh local database when a clean dataset is needed. The ordered migration scripts are one-time scripts and are not idempotent.

## UI disclosure requirement

The UI contract requires every synthetic seed record to carry a visible **Demo data** indicator. Any Phase 2 screen that displays these records must preserve that indicator and must not make the values look like production analytics. See the [UI contract](ui-contract.md) and [Phase 1 handoff](phase-1-handoff.md).

## Expected local use

Use the seed to exercise tenant filtering, campaign and metric displays, import-error states, alert lists, and audit views after those Phase 2 workflows are implemented. The current Phase 1 API maps only `/health`; it does not load this data through an API route yet.
