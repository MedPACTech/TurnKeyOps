# Jobs V1 implementation and release notes

Implementation verified locally on 2026-10-06. This is a working execution workflow in the existing monorepo, not a deployed release. No production data was seeded and no live customer communication was sent.

## Architecture and discovery decisions

The implementation extends the existing `Job` aggregate, Jobs table, `JobService`, and private `JobWorkflowPayloadDto` blob. It does not introduce separate Concrete, Framing, Land Clearing, or Doors Job entities. Existing Leads/Estimates conversion, Calendar entities, tenant memberships, profiles, module access, Bob's policy executor, and iBeam storage/email/SMS are reused.

The former planning model contains concrete-specific checklist/material fields and a separate locksmith technician workflow. Those remain readable for compatibility. New execution data is an optional `JobExecutionDto` payload extension. Legacy mutation paths reject execution Jobs, avoiding conflicting writers. Explicit adoption enables the new workflow for an old Job; adoption retains the original aggregate and source data.

Job and Calendar repositories now preserve physical Azure envelope ETags and use conditional writes. Execution mutations append actor/time activity and store immutable payload versions linked through `PreviousVersionBlobName`. Optimistic concurrency failures do not overwrite another user's Job changes.

## Core, profiles, and tenants

The execution payload owns origin/provenance, trade and service, priority, responsible membership, sold scope, tasks, material/equipment requirements, issues, changes, evidence, completion acceptance, and a downstream billing reference. Dates/customer/site/Lead/Estimate links stay on the established aggregate and sold snapshot.

`JobConfigurationService` supplies trade defaults for concrete, framing, land-clearing, doors-locks, and general work. Profiles define labeled structured text fields, required fields, stage-specific tasks, capability requirements, completion photos, customer acceptance rules, and canonical-state labels. These are profile data rather than trade-specific branches in the lifecycle engine.

Tenant `operational.jobs` configuration controls enabled trades, manual creation, profile overrides, member skills, communication templates/preferences, and `job.*` AI policies. Profile keys may be `trade` or `trade/service`. Each Job receives a deep-cloned profile snapshot: changing company templates does not silently change obligations on existing work. The settings UI currently edits trade-level profiles; service-specific profiles are supported through the configuration API.

One tenant can use multiple profiles with shared customers, sites, Calendar, and workforce. The previous tenant-equals-trade creation restriction was removed. Custom future profile keys retain their string identity even when the legacy enum falls back to General.

## Won handoff and manual/imported work

The accepted Estimate conversion continues to create the existing Job idempotently. It retains the accepted revision/hash/signature, selected operational lines, customer/site IDs, Lead/source/referral, sales ownership, confirmed inputs, terms/timing, Lead context, and attachment references in the accepted snapshot. Private cost/margin data remains redacted. Scope is visible directly in the Job workspace. Material/equipment lines seed requirements with their original quantities and catalog references; a sold deposit condition becomes an evidence-backed release task, not a new accounting implementation.

Source documents remain immutable. Job execution cannot edit accepted pricing. Authenticated file retrieval verifies the tenant and source Estimate attachment path. Retain source Estimate blobs while Jobs reference them.

Policy-permitted manual, warranty, service, internal, other, and imported Jobs require existing tenant customer/site records and an explicit scope. Imports require source system and external ID; tenant/source/external identity produces an idempotent Job ID. This is a single-record integration foundation, not a spreadsheet mapper or bulk history migration. Imported Jobs start as Draft; historical dates/states require a reviewed migration adapter rather than invented activity.

## Lifecycle and readiness

Canonical states are Draft, Planning, Blocked, Ready to schedule, Scheduled, Ready to start, In progress, Paused, Waiting, Ready for completion, Completion review, Completed, Closed, and Cancelled. New enum values were appended; old numeric values were not renumbered. Legacy states map into the canonical presentation. Tenant labels never change transitions or reporting semantics.

`JobExecutionRules` validates the transition graph and schedule/start/completion gates. Readiness combines required tasks, required profile fields, material/equipment availability, unresolved blocking issues, unresolved changes, completion evidence, live schedule events, and detected resource collisions. Owners must explain reopening closed/cancelled work. Reopening creates a new completion revision. Actual start/end are recorded by real transitions; unavailable metrics remain null.

Completed means operational work is finished. Closed requires applicable completion requirements and current customer acceptance. It does not imply payment or financial close.

## Calendar and workforce

Multiple Job events live in the existing Calendar table, with type, UTC window, membership/resource IDs, customer visibility, notes, and scheduled/completed/cancelled status. Existing Calendar pages include these events and link back to the same Job workspace. The Job's start/end pair is only a summary.

Assignments validate active tenant memberships, collective required skill coverage, and overlapping people/equipment events. Recommendations explain availability and skill coverage; they do not claim skills that were never configured. Team labels come from People profiles; tenant member-skill configuration extends the existing membership identity rather than creating another workforce. Legacy technician Job windows are checked too.

Scheduling requires both Jobs and Calendar access. Generic Calendar endpoints cannot bypass execution scheduling validation. Job audit records scheduling intent before Calendar persistence and confirmation afterward. Calendar is authoritative if a later Job summary write fails.

**Calendar concurrency hardening:** `CalendarReservationStore` now commits each event and a tenant guard row in one Azure Table transaction. Concurrent writers must re-read availability when the guard changes; overlapping member/equipment claims cannot both commit. Cancellation releases availability and stale event edits still fail. All Calendar repository saves participate, including older Calendar routes. Job list/detail dates are derived from non-cancelled Calendar events, so a failed secondary Job summary write does not leave the workspace stale. Completed event windows remain available for reporting.

**Remaining scheduling boundary:** legacy Jobs still store their schedule outside Calendar and retain preflight conflict checks. Those writers are not covered by the Calendar transaction guard; migrate/adopt them before relying on uniform atomic reservations. Name-based legacy visits also need explicit member identity. Job/Calendar audit writes are separate; the Calendar workspace recovers accurate dates on reads, but a background summary-repair worker is not implemented.

## Field work, requirements, and changes

The shared Svelte workspace serves BDR, ThinkPink, and Carl Zipf. Owners start with Needs attention; field users start with today's assigned work. Today, Upcoming, Needs attention, All jobs, and Schedule views expose a next action and relevant blockers. Planning, resources, activity history, and Bob controls use progressive disclosure. Existing Tailwind semantic tokens, Lucide, teal/orange accents, and light/dark themes are retained.

Tasks support required/optional stages, owner, due date, dependencies, template provenance, completion actor/time, and evidence. Materials and equipment share an extensible requirement contract with kind, catalog/resource/vendor references, quantity/unit, required date, source, notes, and availability state. Purchasing, stock reservation, fleet maintenance, costing, and payroll are not implemented.

Field staff can record short dictated text, capture photos/files, complete checklist items, raise issues, and capture change requests. Evidence stays private and associated with the Job. Planning/field photos do not count as required completion photos unless explicitly uploaded for completion.

Issues have severity, owner, evidence, resolution, and audit. Changes have origin, requester, scope/schedule/pricing impact, evidence, approval, and validated states. A priced change requires a separately accepted Estimate for the same customer/site; its revision/hash is retained. Jobs never invent new approved prices. The UI currently links an existing change Estimate rather than generating a dedicated change-estimate editor.

The PWA provides installation metadata and an offline recovery page. It caches no customer records and submits no edits while offline. There is no background edit queue or offline sync; the UI explicitly avoids claiming that anything has been submitted.

## Bob and customer communications

Job summary, readiness, recommendations, note structuring, tasks, material requirements, activity, change drafts, schedules, transitions, and notifications use the existing Bob operation/approval pipeline. Target-bound approval, user module access, tenant policy, validation, concurrency, and audit remain enforced at execution time. Jobs policies override the shared executor's defaults without creating another automation engine.

Note structuring is bounded deterministic suggestion extraction, preserving the original note and requiring review. It does not infer authoritative quantities, prices, or completion from ambiguous speech. There is no new speech recognizer or semantic language-model integration. Proactive weather-based rescheduling and background coordination are not implemented.

`JobDeliveryService` uses existing iBeam email/SMS transports, authoritative customer contacts, configured templates/preferences, and the policy executor. A tenant-scoped `JobNotifications` envelope freezes the recipient/body and records actor, Job, template and timestamps. A conditional prepared → sending claim allows only one provider attempt for a stable Job/version/channel/template identity. Replays return the receipt. Provider timeouts, process interruption and lost acknowledgements never trigger an automatic resend; sending/unconfirmed outcomes require provider-history review. The workspace exposes receipt status separately from successful customer delivery. A provider acceptance is not a delivery receipt.

This is a durable dispatch/receipt foundation, not an unattended retry worker. Provider reconciliation, delivery webhooks, and live authorized transport validation remain rollout work. No live messages were sent during verification.

## Completion, acceptance, and security

Completion gates use the Job's frozen profile, required tasks/fields/photos, requirements, issues, and changes. Customer completion acceptance is separate from Estimate acceptance. Staff record an onsite signer, exact statement, signature/confirmation, exceptions, accepted canonical state, actor, server timestamp, completion revision, and hash of the completion data. Mutating accepted tasks, requirements, issues, changes, trade data, or evidence invalidates the current signoff revision. A remote tokenized customer-signature portal is not part of this implementation.

All workspace operations require authenticated tenant context and Jobs read/write authority at the service boundary, including Bob. Jobs access is independent of Leads and Estimates. Priced-change lookup additionally uses Estimates authorization; scheduling uses Calendar authorization. Configuration changes require an owner. Customer/site/member references are tenant validated. Private file endpoints authorize the Job before serving content. This is tenant/module isolation; field queue filtering is a convenience, not row-level assignment-only authorization.

## Reporting and integration contracts

Canonical state, real dates, schedule events, activities, blocker reasons, changes, and acceptance times form the reporting foundation. The metrics endpoint returns actual stored data and nullable durations, not synthetic utilization/profitability. Workforce optimization by territory, historical workload, and productivity needs additional authoritative data. A downstream billing reference and requirement/resource identities provide extension points for Finance, Inventory, Purchasing, Equipment, and Time without implementing those modules here.

## Compatibility and rollout

- No destructive migration or bulk rewriting of existing Jobs is required. Optional payload fields deserialize old records; enums retain old values.
- Existing BDR/ThinkPink legacy Job screens remain under `/admin/jobs/legacy`. New default Jobs pages use the shared workflow.
- Existing invoice/deposit behavior remains on legacy paths. Execution Jobs use evidence-backed release gates and do not create new financial behavior.
- Review `operational.jobs`, membership capabilities, completion requirements, communication templates, and AI policies before enabling operational use. Defaults are starting templates, not evidence that a crew is qualified.
- Configure the existing storage/transport services; no new external platform is required. Preserve immutable Job payloads and referenced Estimate attachments in retention policies.
- No deployment, production migration, live customer transport test, or production data changes were performed.

## Verification

- API suite after continuation: 300 passed; four storage checks skipped in the default run. Added startup permission-catalog regression, schedule recovery, duplicate-notification, timeout, and lost-acknowledgement tests.
- Isolated Azurite storage suite: four passed, including physical Job/Calendar ETags, tenant isolation, stale writes, concurrent people/equipment reservations, cancellation and adjacent windows.
- Client unit suite: 52 passed.
- `npm run check`: zero errors/warnings; production `npm run build`: passed.
- Jobs Playwright: 16 passed across desktop/Pixel 7, light/dark axe scans, keyboard navigation, field mutations, read-only UI, completion acceptance, settings, manifests/auth, and honest offline recovery.
- Leads Playwright: ten passed.
- Estimates Playwright: 16 passed in the final run. Its read-only preview exposed a pre-hydration click race; preview now remains disabled until mounted, matching the mutation controls. An intervening run also failed keyboard navigation while the build was running; the clean run passed after the build stopped.
- `git diff --check`: passed.

Browser suites use explicit local fixture APIs. A separate live local check verified Jobs/choices/configuration and Calendar pages, adopted the pre-existing tagged sample Job while preserving accepted revision/sold scope, and rejected a stale write. These are local checks, not staging or transport delivery validation. API service/storage tests verify backend behavior separately. Axe plus keyboard/mobile checks are evidence for tested screens, not a claim of a complete independent WCAG audit.

Run from repository root:

```sh
dotnet test api/TurnKeyOps.Authorization.Tests --no-restore
TKO_STORAGE_TESTS=1 dotnet test api/TurnKeyOps.Authorization.Tests --no-build --filter 'Category=StorageIntegration'
```

Run from `client`:

```sh
npm run check
npm run build
node --test --experimental-strip-types tests/*.test.ts
npx playwright test --config playwright.jobs.config.ts
npx playwright test --config playwright.leads.config.ts
npx playwright test --config playwright.estimates.config.ts
```

Keep storage integration tests separate from the other API tests: enabling storage tests in the combined run exposed shared static EntityKeyPolicy configuration interference in existing fixtures. Both isolated runs pass; the combined configuration is not claimed as passing.

## Hubbsly and next workstream

Epic: **Jobs V1 — AI-First Job Execution**, `b86ce660-3023-4c62-8269-7ed6ac3ccd9c`.

| Card | Scope | Status |
| --- | --- | --- |
| TKO-0054 | Universal core, profiles, lifecycle, readiness | Done locally |
| TKO-0055 | Shared Calendar events and qualified assignments | In progress: legacy schedule migration/rollout |
| TKO-0056 | Tasks, requirements, field activity, controlled changes | Done locally |
| TKO-0057 | Shared workspace and field PWA | Done locally; recovery-only offline mode |
| TKO-0058 | Bob operations and customer communication policy | In progress: provider reconciliation/validation |
| TKO-0059 | Completion, acceptance, reporting, import foundation | Done locally; single-record import foundation |
| TKO-0060 | Tenancy, authorization, accessibility and regression verification | Local checks passed; staging validation remains |

The epic remains in flight for operational hardening and rollout. Recommended next workstream: **Scheduling and delivery reliability**—legacy schedule migration, background summary repair, provider delivery reconciliation, and a real API-backed staging acceptance run. Then build bulk migration tooling and optional remote completion signing. Offline edits, advanced crew optimization, semantic dictation, and proactive weather automation should be separately scoped rather than implied by this release.

## Local continuation and permission catalog

The first live request exposed missing `jobs.read` / `calendar.read` registration despite passing mocked service tests. Both keys are now registered and mapped to owner/admin roles which already have the corresponding write grants. Separate per-user module restrictions remain mandatory. Automatic approval review declined broadening Staff/Member system defaults, so those grants were not changed: review explicit operational role grants before field rollout. Do not mistake a module checkbox for an additional role grant.

The existing local Carl Zipf tagged sample Job was explicitly adopted for verification; its accepted Estimate revision 1 and immutable sold scope were retained. No production records, deployments or messages were involved. Local API was rebuilt/restarted and client check/build passed.
