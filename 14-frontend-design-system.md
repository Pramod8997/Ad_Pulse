# 14 — Frontend Product and Design System

## 1. Experience objective

Build a serious analytics workspace that looks like a carefully art-directed B2B product—not a generic AI-generated admin template. The interface should feel precise, calm, dense enough for campaign analysis, and fast to scan. Visual polish must come from hierarchy, spacing, typography, consistent states and useful information—not decoration.

## 2. Frontend stack

- Vue 3 + TypeScript + Vite.
- Vue Router for route structure and protected pages.
- Pinia only for durable client state such as signed-in identity, selected workspace and UI preferences.
- TanStack Vue Query for server state, caching, invalidation and mutation lifecycle.
- Tailwind CSS for layout/utilities with an owned token layer.
- shadcn-vue / Reka UI primitives for accessible menus, dialogs, tabs, popovers, tooltips and command surfaces. Customize the component source and tokens; do not ship the default demo appearance unchanged.
- Lucide Vue icons for consistent line icons. No emojis, emoji icon fonts or decorative Unicode symbols.
- Apache ECharts for time-series and budget pacing visualizations. Wrap only the charts actually used.
- Native HTML form semantics and browser behavior before adding form/date-picker libraries.

Dependencies are subject to Ponytail's decision ladder: inspect the existing package first, reuse what is installed, and justify each new package in the PR.

## 3. Visual direction: “Quiet Precision”

A light, neutral workspace with graphite typography, subtle borders, restrained teal for primary actions, and semantic amber/red/green only for status. Avoid gradients, glassmorphism, neon, glows, blurred blobs, gratuitous illustrations, emoji, excessive rounded cards, oversized hero headings and fake activity indicators.

### Design tokens

| Token | Value | Use |
|---|---|---|
| `--app-bg` | `#F5F6F8` | App canvas |
| `--surface` | `#FFFFFF` | Panels and cards |
| `--surface-subtle` | `#F8FAFC` | Table headers and quiet fills |
| `--text-primary` | `#182230` | Main text |
| `--text-secondary` | `#667085` | Supporting labels |
| `--border` | `#E4E7EC` | Dividers and control borders |
| `--accent` | `#176B64` | Primary actions, selected navigation |
| `--accent-hover` | `#12564F` | Hover/pressed primary action |
| `--success` | `#16794A` | Healthy / resolved |
| `--warning` | `#A15C07` | Attention / pacing risk |
| `--danger` | `#B42318` | Critical / failure |
| `--info` | `#175CD3` | Informational status |
| `--focus` | `#2E90FA` | Visible keyboard focus |

Use semantic tokens, not hard-coded component colors. Status must have text labels or icons in addition to color.

## 4. Typography and layout

- Font stack: `Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`; bundle fonts only if licensing and repository policy are confirmed.
- Body: 14px/20px baseline; dense table text may use 13px but must remain readable.
- Page title: 24–28px, semibold. Section headings 16–18px. KPI value 24–30px.
- Numeric values use tabular numerals; align currency and metric columns consistently.
- 8px spacing rhythm. Page content max width around 1440px; default 24px desktop page padding, 16px tablet, 12px mobile.
- Desktop shell: 248px left navigation, 64px top bar, flexible content. Sidebar can collapse to a compact icon rail only if labels remain available accessibly.
- Use a 12-column content grid at desktop; stack at tablet/mobile. Avoid horizontal scrolling for the main dashboard; data tables may use a deliberate overflow container.
- Radii: 6px controls, 8px cards/panels, 10px dialogs. Shadows are subtle and used only for overlays, not every card.

## 5. Application shell

### Left sidebar
- Wordmark: `AdPulse` in text; do not invent a decorative logo mark.
- Workspace switcher with organization name and current role.
- Primary navigation: Overview, Campaigns, Alerts, Imports, Audit log.
- Bottom area: Settings, help/documentation link, user menu.
- Selected route uses a quiet tinted surface and a 2px accent marker; not a large colorful pill.

### Top bar
- Breadcrumb or page context on left.
- Global search/command trigger with keyboard hint.
- Current reporting period selector and refresh timestamp on relevant analytics pages.
- Notification indicator only if it is connected to real state.
- Profile menu with account and sign-out controls.

## 6. Required screens

### Overview `/app`
1. Title and date range filter, with timezone/reporting date clearly visible.
2. KPI strip: Spend, Budget consumed, CPC, CPA, ROAS. Each card includes a value, comparison period, direction and accessible trend label. Do not manufacture comparison data; hide comparison when no valid period exists.
3. Main trend panel: daily spend and revenue with series toggles and clear axes/tooltip.
4. Budget pacing panel/table: campaign, spent, planned-to-date, pacing %, remaining budget and status.
5. Priority alerts panel showing severity, campaign, rule/metric, short explanation and age.
6. Campaign performance table with sortable columns and pagination.

### Campaigns `/app/campaigns`
- Search, status filter, date filter and “New campaign” action.
- Table: campaign, status, budget, spend, CPC, CPA, ROAS, last metric date.
- Row action menu; clicking name opens detail page.
- Create/edit form groups identity, schedule, budget/currency and optional targets. Tenant identity is not a form field.

### Campaign detail `/app/campaigns/:id`
- Header with campaign name, status, budget and date range.
- Tabs: Overview, Performance, Alerts, Activity.
- Overview contains spend pacing chart, KPI strip and recent alerts.
- Performance contains time range and KPI series; do not mix incompatible currencies.
- Activity shows real audit/activity records only.

### Alerts `/app/alerts`
- Filter by status, severity, metric, campaign and date.
- Table/list columns: severity, alert title, campaign, detected metric, observed vs baseline, detected time, status.
- Detail drawer or dedicated detail view includes explanation, rule ID, scoring version, evidence window, observed/baseline values, confidence/data sufficiency and recommended checks.
- Acknowledge, resolve and reopen actions require confirmation/reason where specified and show mutation outcome.
- Do not call heuristic score a probability.

### Imports `/app/imports`
- Guided but compact workflow: download CSV template, select file, map/validate columns only if mapping is genuinely supported, preview counts/errors, confirm import, show result.
- State file limits and expected date/currency semantics.
- Show row-level rejection report without exposing other tenant data.

### Audit log `/app/audit`
- Filter by date, actor, action and entity.
- Read-only, tenant scoped, paginated, stable sort.
- No arbitrary audit-event creation from browser.

### Settings `/app/settings`
- Tenant settings only where implemented; member/role management is tenant-admin only.
- Do not render nonfunctional toggles or pretend settings persist.

## 7. Interaction states

Every page and major component must implement:
- Loading state: skeletons matching actual layout; no endlessly spinning page.
- Empty state: explain why the list is empty and give a relevant next action, without decorative illustration.
- Error state: concise message, retry action where safe, correlation ID for support if available.
- Partial state: distinguish missing metrics from zero metrics.
- Disabled state: explain why an action is unavailable when non-obvious.
- Success state: restrained toast/inline confirmation; do not overuse animation.
- Destructive or irreversible action: confirmation dialog and clear consequences.
- Stale data: display last successful refresh; do not silently imply live data.

## 8. Accessibility and responsive behavior

- Target WCAG 2.2 AA for core flows; use semantic landmarks, heading order, labels, keyboard focus and screen-reader names.
- Every icon-only button has an accessible name and tooltip.
- Dialogs trap focus, return focus to opener and support Escape when appropriate.
- Charts have a text/table equivalent for important values; color is never the only encoding.
- Minimum interactive target .44x.44 CSS px where practical; respect reduced-motion preference.
- Mobile navigation becomes a labelled drawer; KPI cards stack; filters move into a filter panel; tables expose priority columns first.

## 9. Motion and polish

- Use short 120–180ms transitions for hover/focus/open/close only.
- No parallax, bounce, perpetual animation, animated gradients, count-up numbers or motion that delays interaction.
- Use consistent border, spacing and alignment. Ensure all loading placeholders settle to real content without layout jumps.
- Avoid invented avatars, fake customer logos, fake online dots, fabricated recent activity or unsubstantiated “AI insights”.

## 10. Quality bar / visual acceptance checklist

- Every route is implemented and connected to real API behavior or clearly marked as an empty/error state.
- No placeholder links, inert buttons, fake counters or hardcoded production-looking analytics.
- All synthetic seed data carries a visible Demo data indicator.
- Desktop 1440x900, laptop 1366x768, tablet 768px and mobile 390px layouts are checked.
- Keyboard-only smoke test covers navigation, search, campaign create/edit, CSV import, alert acknowledge/resolve and filters.
- No horizontal overflow outside intentionally scrollable tables/charts.
- No console errors, broken chart resize, hydration mismatch or uncontrolled state warnings.
- Colors, typography, spacing, status labels and controls are consistent on all pages.
- Screenshot comparisons are captured from the real running app, not AI-mocked screen renderings.
