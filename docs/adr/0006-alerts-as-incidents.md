# ADR 0006: Extend Alerts into Incidents

- Status: accepted for the first operational workflow slice
- Date: 2026-11-04

## Context

AdPulse already persists tenant-scoped alerts with evidence, severity, fingerprints, lifecycle actions, and append-only audit events. The new product positioning requires ownership, investigation notes, timelines, and resolution tracking. The repository has no demonstrated requirement to group multiple alerts into one case.

## Decision

Extend the existing `Alert` as the initial incident source record. Add only the missing workflow state and metadata, and reuse the tenant-scoped append-only `AuditEvent` store for timeline entries and immutable investigation notes.

The existing `Alert.Status` values and routes remain backward-compatible. New incident workflow fields are additive, nullable where required for existing rows, and exposed through an additive projection rather than silently renaming `open` to `new`.

Owner subjects are accepted only after server-side membership validation against the authenticated tenant context. Timeline payloads are bounded and returned through an allowlisted DTO; raw audit JSON is never exposed.

## Alternatives rejected

- **New `Incidents` table:** no current evidence requires multi-alert grouping; it would duplicate lifecycle, evidence, tenant, and audit behavior.
- **Editable notes table:** not required for append-only investigation notes; introduce one only if editing, deletion, search, or versioning becomes a real requirement.
- **Replacing alert status:** would break strict API, TypeScript, and persisted-data consumers.

## Consequences

- Existing alert routes, scoring, deduplication, campaign relationships, and audit integrity remain in place.
- The first workflow is intentionally one alert to one operational case.
- SQL changes are additive and require an ordered migration after the current scripts.
- A future multi-alert incident aggregate requires a new reviewed ADR and migration rather than an implicit expansion of this model.
