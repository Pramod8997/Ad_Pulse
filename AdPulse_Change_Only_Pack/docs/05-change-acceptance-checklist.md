# 05 — Change Acceptance and Regression Checklist

## Before implementation
- [ ] Git status checked and recovery point created.
- [ ] Existing build/test baseline recorded.
- [ ] Current feature inventory completed.
- [ ] Keep/Modify/Extend/Add/Retire matrix reviewed.
- [ ] Impacted code, DB, API and frontend mapped.
- [ ] Migration and rollback plan documented.
- [ ] No new scaffold or duplicate module proposed without cause.

## During implementation
- [ ] Existing modules reused where appropriate.
- [ ] Changes remain small and independently reviewable.
- [ ] Database migrations are additive where possible.
- [ ] Existing API contracts remain compatible or have a documented versioning plan.
- [ ] Existing tenant security preserved.
- [ ] No credentials, raw uploads or personal data added to logs.
- [ ] Ponytail review performed when supported.

## Regression checks
- [ ] Existing campaign CRUD works.
- [ ] Existing budget and metric views work.
- [ ] Existing anomaly scoring works for established test fixtures.
- [ ] Existing alert listing/detail/actions work.
- [ ] Existing import workflow works.
- [ ] Existing audit history works.
- [ ] Existing authentication and tenant isolation work.
- [ ] Existing frontend routes and navigation work.
- [ ] Existing CI pipeline remains functional.
- [ ] Existing deployment/local startup works.

## New workflow checks
- [ ] Alert/incident owner assignment.
- [ ] Acknowledge/investigate/mitigate transitions.
- [ ] Resolution requires a summary.
- [ ] False positive requires a reason.
- [ ] Incident timeline/audit events.
- [ ] Signal grouping does not delete source signals.
- [ ] Change-event context is labelled as correlation, not cause.
- [ ] Cross-tenant and cross-workspace access denied.
- [ ] Repeated scoring/import remains idempotent.

## Completion
- [ ] Actual tests and outputs recorded.
- [ ] Changed files listed.
- [ ] Known failures distinguished from baseline failures.
- [ ] Deferred features explicitly listed.
- [ ] No unsupported production-readiness claim.
