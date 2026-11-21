# Phase 1 UI contract

Status: maintained contract for the existing Phase 2 implementation. Routes, client and token shape remain backward-compatible.

## Route map

| Route | Screen | Access |
|---|---|---|
| `/app` | Overview | tenant member |
| `/app/campaigns` | Campaign list/create | tenant member |
| `/app/campaigns/:id` | Campaign detail | tenant member |
| `/app/alerts` | Incident inbox and investigation detail | tenant member |
| `/app/imports` | CSV import workflow | tenant member with import permission |
| `/app/audit` | Audit log | tenant member with audit permission |
| `/app/settings` | Tenant settings | tenant admin |

The frontend may use route guards for navigation experience only. The ASP.NET API remains the authorization boundary and derives tenant scope from the validated identity plus server-side membership.

## API client and token strategy

- One typed client owns base URL, bearer attachment, JSON parsing, correlation ID capture and `application/problem+json` handling when workflow endpoints are added.
- Access tokens are supplied by the OIDC browser integration and kept out of local storage. The client must not accept a tenant ID override or custom authorization header.
- TanStack Vue Query owns server state, cache keys and invalidation. Pinia is limited to signed-in identity, selected workspace and UI preferences.
- Query keys include the authorized workspace context only as a cache partition; they never grant access.
- Missing numeric data stays `null` through the client and UI. Zero is rendered only when the API returns zero.
- The existing alerts route is also the incident workflow: ownership, incident state, bounded investigation notes, evidence, resolution metadata and a safe timeline are rendered in the existing list/detail flow.
- Existing alert status values and lifecycle routes remain supported. New incident state actions require server confirmation; resolution and false-positive actions require a summary/reason.

## Quiet Precision tokens

The implementation uses semantic CSS variables aligned with `14-frontend-design-system.md`:

```css
--app-bg: #F5F6F8;
--surface: #FFFFFF;
--surface-subtle: #F8FAFC;
--text-primary: #182230;
--text-secondary: #667085;
--border: #E4E7EC;
--accent: #176B64;
--accent-hover: #12564F;
--success: #16794A;
--warning: #A15C07;
--danger: #B42318;
--info: #175CD3;
--focus: #2E90FA;
```

The shell is a 248px navigation rail plus a 64px top bar on desktop, with labelled drawer navigation on mobile. Components must use semantic HTML, Lucide icons, visible focus, keyboard operation, labelled icon-only controls and explicit loading/empty/error/partial/stale states. Incident detail uses the same tokens and drawer/dialog patterns; no decorative or fabricated data is used.
