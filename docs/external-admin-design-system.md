# External Admin design system

The BDR, Think Pink, and Carl Zipf External Admin screens share the approved TurnKeyOps.AI light/dark palette. The implementation lives in `client/src/lib/styles/admin-theme.css` and is scoped to `.admin-theme`; public sites, the platform console, and the legacy `admin/` app retain their existing appearance.

## Implementation

- `AdminShell.svelte`: light/charcoal navigation, Lucide module icons, active-route semantics, collapse control, mobile navigation, profile/voice panel, sign-out, skip link, appearance preferences.
- `AdminIcon.svelte`: common module and trade icon mapping.
- `AdminDrawer.svelte`: native modal dialog, Escape/backdrop close, focus wrapping, focus restoration, independently scrolling body.
- `AdminWorkspace.svelte` and `AdminContextRail.svelte`: themed surfaces, metrics, selection, and shared details drawer.
- `ContactsManagement.svelte`, `PeopleManagement.svelte`, `TenantUserManagement.svelte`, and `IssuedQuoteLink.svelte`: themed forms, states, and actions.
- Existing Svelte route components under `client/src/routes/{bdr,thinkpink,carlzipf}/admin/`: semantic surfaces, text, statuses, controls, and Lucide icons. Routes that already use shared components or semantic tokens inherit the migration.
- `playwright.admin-ui.config.ts` and `tests/admin-ui/`: isolated rendering of real components with explicitly synthetic data. The harness is outside the application's routes and does not bypass production authentication. `quality-gates.yml` runs it alongside existing browser release gates.

Teal represents navigation, selection, links, focus, and intelligence. Burnt orange represents primary workflow actions. Neutral secondary and text-labeled semantic status treatments work in both themes. Existing token aliases remain so module styling has one semantic source of truth. Tenant interaction colors no longer override the shared palette.

Appearance initially follows the operating system. Explicit light/dark choices persist under `tko-admin-theme` in local storage. Choosing “Use system setting” removes the override and resumes live system changes. Storage failure does not prevent a theme change for the current visit.

## Preserved behavior

`ExternalAdminLayout` and `getExternalAdminConfig` still compose tenant navigation and filter module permissions. No authentication hooks, authorization policies, tenant mappings, server loaders/actions, API contracts, or request lifecycle logic changed. The existing dashboard metrics, links, grouping, and role-driven content remain intact. Requests were not renamed to Leads. Bob voice cookies, logout destinations, profile access, and mobile/collapsed navigation remain available. No charts were replaced and no charting dependency was introduced.

## Verification

Commands run from `client/`:

| Check | Result |
| --- | --- |
| `npm run check` | 0 errors, 0 warnings |
| `npm run build` | Production build succeeds |
| `npm run test:session` | 18 tests pass, including the BDR receipt/tenant regression |
| `npm run test:carlzipf` | 26 tests pass |
| `npm run test:e2e` | 7 tests pass: intake, authorization, OTP, and public-page axe checks |
| `npx playwright test --config playwright.people.config.ts` | 9 tests pass: contact/user persistence, permissions, denial/session handling, deletion cancellation, and axe |
| `npm run test:admin-ui` | 56 tests pass across desktop/mobile and light/dark |

The new suite renders all three tenant shells, BDR and Think Pink dashboards, Carl Zipf Requests, the BDR Requests detail view, estimate review, invoice/job empty states, settings, Bob, shared controls, contact creation, and user creation. It checks WCAG 2 A/AA and 2.1 AA axe rules, theme persistence/system changes, permission-filtered destinations, active navigation, collapse, drawer focus/Escape/restoration, mobile page overflow, primary touch target size, and token contrast. No axe violations are suppressed in this suite.

Measured contrast ratios:

| Pair | Light | Dark | Minimum |
| --- | ---: | ---: | ---: |
| Muted text / surface | 5.51 | 7.93 | 4.5 |
| CTA white text / orange | 5.02 | 5.02 | 4.5 |
| CTA white text / hover | 7.31 | 7.31 | 4.5 |
| Focus outline / surface | 5.33 | 7.20 | 3 |
| Input boundary / surface | 3.78 | 5.18 | 3 |

The approved subtle-text token is retained as a palette reference; ordinary small text uses the AA-safe muted token. Status colors have separate text/surface variants. Inputs use stronger boundaries than decorative card borders. Reduced-motion preferences suppress admin animations and transitions. A pre-existing 85%-opacity warning in Requests was made fully opaque after axe measured it below AA.

## Scope and remaining verification

No known legacy hard-coded surface/tenant color styling remains in the targeted External Admin route components. The legacy `admin/` application, `PlatformAdminShell.svelte`, public marketing pages, and locksmith field workspace were deliberately outside this migration. Website editor content previews and uploaded customer media retain their own content appearance.

The automated result is scoped to the states exercised above, not a blanket accessibility certification of every possible customer record, uploaded document, integration error, or browser/assistive-technology combination. Dense populated job/invoice lifecycle states and full screen-reader review are sensible follow-up work in Hubbsly. The separate Leads/domain redesign also remains outside this change. No Hubbsly cards were created by this migration.
