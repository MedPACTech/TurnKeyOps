# Leads V1 architecture and migration

## Discovery (2026-10-05)

The active application is `client` (SvelteKit); `admin` is legacy. Existing local session/request changes are preserved. Azure Tables repositories use iBeam envelope persistence and tenant partitions. QuoteRequest owns the existing public intake contract, immutable submitted payload, attachments, qualification and site-visit timeline. Customer is the reusable person/company record. Estimate and Job already preserve snapshots and conversion history. CalendarEvent is the scheduling source of truth. BobOperationsService already persists proposals, approvals, executions and audit records. iBeam email/SMS packages and tenant communication profiles exist; no second transport will be introduced. Carl Zipf has a technician PWA; shared Leads views must also work on touch screens.

This checkout has a persisted role/permission catalog and IRoleAccessService, but existing Requests/Estimates controllers still use broad TenantStaff authorization. Leads adds independent leads.read/leads.write keys to that architecture; product packaging does not grant permissions. Employee access remains separate from contact access grants.

## Migration decision, before implementation

Add an independent, tenant-partitioned canonical Lead representing ONE opportunity. CustomerId is a many-to-one relationship, never a Lead identity. IntakeRequestId links retained QuoteRequests; deterministic Lead IDs equal intake IDs only for the bridge. No request/customer/history deletion, table renaming, or production rewrite. Bridge retries must never overwrite an existing Lead's workflow. Existing requests can be reconciled explicitly and repeatedly; public intake creates its request first so a retry can repair a partial bridge failure. Existing Requests URLs remain compatibility tools while navigation moves to Leads.

Canonical stages are stable strings. Display labels, qualification fields, won/lost reasons, assignment rules and per-action AI policy live under operational tenant settings. Trade defaults overlay platform defaults; tenant overrides overlay trade defaults. A Lead selects one trade profile, while configuration supports multiple profiles. Source/referral, attribution, timestamps, estimate/job IDs, activities and qualification remain on the Lead after closing or conversion.

Mutations must check active tenant and service-level permissions, validate linked records inside that tenant, preserve server-owned history, and record the actor. Bob reuses those same services and the existing action executor; policy is checked at execution time as well as proposal time. A tenant policy never grants a permission. Sending or scheduling must not report success when the provider fails.

## Rollout and rollback

Deploy API and additive permission seeds before client navigation. Verify permission mappings for custom tenant roles; do not infer Leads rights from existing operations rights. Reconcile historical requests per tenant using the authenticated migration operation and compare counts/source links. Keep old endpoints and data for old public clients/field clients. Rollback client navigation independently; retain new tables and links. No production deployment or production data migration is performed by this implementation task.

## Validation

Cover tenant isolation, denied/read/write access, intake replay, manual creation, multiple opportunities per customer, explicit duplicate links, qualification, lost reopening, stable labels, downstream history and Bob policy/authority. Run client check/build/tests, API tests, and Playwright/axe on desktop/mobile and both themes. Record limitations and evidence here and on Hubbsly; unfinished acceptance criteria must remain open.

## Implemented integration details

- Current external-admin estimating uses `QuoteEstimateService` packets, while the older standalone application also has `EstimateService`. New Leads prepare an unpriced packet through the current service; existing trade pricing guards remain intact. QuoteEstimate Lead/Customer IDs survive revisions. Sent/approved packet events update the canonical Lead. Won handoff requires the customer's approved packet and creates an idempotent linked Job through `JobService`.
- Activity uses immutable private `lead-activity` blobs and a conditional Azure Table pointer update, following `JobWorkflowPayloadStore`. Failed conditional writes may leave unreferenced snapshots; retention cleanup must not remove referenced snapshots.
- Lead settings updates preserve unrelated operational values and server-only secret references. New action policies are checked at proposal and execution. The contextual assignment UI uses the existing Bob proposal/approval executor.
- Module access defaults grant Leads to owner/admin; existing custom roles need explicit read/write grants. The client admits other employee roles only to Leads routes, with the API remaining authoritative. Other admin routes retain their current admission rules.
- `/api/public/leads/{tenantSlug}` is an additive alias of the existing rate-limited, honeypot-validated intake contract. Browser clients use the public site identifier; private integrations use authenticated `/api/leads` with employee/integration permissions. No browser API secret is introduced. Intake attribution accepts bounded campaign/landing-page/form metadata.
- The shared Leads PWA uses the existing recovery-only caching pattern. It does not cache private records or claim offline mutations succeeded. Dictation uses the device keyboard. Advanced offline synchronization stays on existing Hubbsly TKO-0028.
- `lead-import.ts` provides provider-neutral row mapping and a review-only preview contract for CSV/Excel/CRM adapters. It never silently creates or merges imported records.

## Release evidence (2026-10-06)

Implemented locally against base `771d888`; no commit, PR, deployment or production migration was created. Pre-existing session, Requests and Carl Zipf edits were preserved.

| Check | Result |
| --- | --- |
| .NET authorization/service suite | 183 passed; zero errors; six existing nullable warnings outside Leads |
| Client `npm run check` | Zero errors, zero warnings |
| Client production build | Passed |
| Client Node tests | 42 passed |
| Leads Playwright | 8 passed: desktop + Pixel 7, light/dark detail, keyboard, manual capture/reopen, offline recovery |
| Scoped axe WCAG 2 A/AA + 2.1 AA | Zero violations in the tested Leads detail surface in both themes/devices |
| Existing public intake Playwright against real API/Azurite | 5 passed, including attachments, tenant/auth boundaries and mobile accessibility |
| `git diff --check` | Passed |

The Leads browser suite uses an isolated contract fixture. Public intake regression uses real local API/storage on separate ports to avoid interrupting existing services. Tests do not demonstrate live SMS/email delivery or physical-device installation. Offline tests verify only the recovery document is cached; private Lead records are not cached.

## Main implementation locations

- `api/TurnKeyOps.Lib/Dtos/LeadDtos.cs`, `Entities/Lead.cs`: canonical opportunity, configuration, qualification and history contracts.
- `api/TurnKeyOps.Repositories/LeadRepository.cs`: tenant-partitioned conditional persistence.
- `api/TurnKeyOps.Services/LeadService*.cs`, `LeadActivityStore.cs`: workflow, permissions, customer links, communications, files, scheduling and history.
- `LeadIntakeBridge.cs`, `LeadConfigurationService.cs`, `LeadWorkflowEvents.cs`: compatibility, layered tenant/trade configuration and estimate events.
- `BobLeadActionProvider.cs`, existing `BobOperationsService.cs`: policy-gated actions using existing audit/approval infrastructure.
- `api/TurnKeyOps.API/Controllers/LeadsController.cs`, `LeadErrorsAttribute.cs`: private API and expected workflow error responses; public alias stays on the existing public intake controller.
- `client/src/lib/components/leads/LeadsWorkspace.svelte`, `LeadSettings.svelte`: shared workflow UI; `client/src/lib/server/leads.ts` and `lead-settings.ts`: session-bound server actions.
- `client/src/lib/lead-import.ts`: provider-neutral mapping/preview foundation.
- `client/static/leads-service-worker.js`, `client/static/leads/`, `lead-manifest.ts`: online-first PWA recovery.
- `api/TurnKeyOps.Authorization.Tests/LeadServiceTests.cs`, existing Bob tests, `client/tests/leads.test.ts`, `client/tests/leads-browser/`, and isolated Playwright configs: verification.

## Screens and endpoints

All three tenant prefixes (`/bdr`, `/thinkpink`, `/carlzipf`) now expose `/admin/leads`, `/admin/leads/[id]`, `/admin/leads/settings`, a manifest and authenticated file routes. Queue is the default; pipeline and list are alternate views. Detail progressively exposes qualification, source/referral, customer linking, notes, files, communication drafts/send, scheduling, closing/reopening and estimate/job handoff. Existing Requests routes remain accessible for original intake history. Carl Zipf displays the linked Job ID because that tenant does not have the general admin Jobs screen.

Private `/api/leads` operations include configuration, metrics and explicit intake reconciliation. `/api/public/leads/{tenantSlug}` shares the existing public Request contract. No secret is placed in the browser.

## Hubbsly tracking

Epic: **Leads V1 / AI-First Lead-to-Won Workflow** (`4aef8c92-4a89-4783-be38-2f66bc6b00ed`), in flight. Cards TKO-0032 (domain/config/permissions), TKO-0033 (intake bridge), TKO-0034 (shared/mobile workspace), TKO-0035 (Bob/communications), TKO-0036 (estimate/job/metrics), TKO-0037 (migration/import/verification) contain implementation evidence and remaining work. They remain open for review/rollout rather than claiming production delivery. Existing offline card TKO-0028 is reused. No commit or PR link has been fabricated.

## Remaining scope and architectural risks

1. Production rollout still needs explicit role-grant verification, tenant configuration review, historical reconciliation and count/link comparison. Reconciliation is additive; it does not rewrite an already-created Lead or overwrite original intake.
2. QuoteRequest, Lead, estimate, calendar and Job writes are separate transactions. Stable IDs/version reservations reduce duplicate actions, but a process failure between writes can leave a source event ahead of Lead history. Add an outbox with replay/reconciliation before unattended automation. Communications can be accepted by a provider before the final activity write; inspect provider history for unconfirmed sends rather than blindly retrying.
3. New estimate packets preserve explicit Lead/Customer IDs. Existing signed packets are left immutable and retain their relationship through the intake ID; historical backlink migration needs signature-aware review. Detail retains original intake and estimate history rather than flattening every downstream revision into Lead activity.
4. Bob has deterministic guidance, registered action providers, permissions and per-action policy enforcement. Free-text extraction, background autonomous intake processing and advanced assignment availability/weighting are future work. Human approval remains required for stage automation until deterministic transition policy exists.
5. Import is mapping/preview only. CSV/Excel parsing, connector ingestion, reviewed commit and import-run deduplication are not implemented. Reporting is an API foundation, not a new dashboard.
6. PWA recovery works in desktop/mobile Chromium tests. Offline edits/synchronization, push reminders and physical iOS/Android installation certification are follow-ups. Dictation relies on the device keyboard.
7. Live iBeam provider smoke tests, delivery callbacks, cross-record failure-injection tests and end-to-end estimate signing/job conversion in a configured tenant remain rollout validation. The scoped axe results do not certify every existing admin screen.
8. Blob snapshots can become unreferenced after failed conditional writes; retention must preserve referenced histories. Current workspace queries load a tenant's Leads; pagination and indexed reporting should precede large-volume imports.
9. Existing non-Leads modules retain their broad route admission rules. Custom sales roles can use explicitly granted Leads permissions, but a full module-permission migration for Estimates/Jobs/Calendar is separate work.

Recommended next workstream: **Estimate/proposal orchestration and reliable workflow delivery**—add an outbox/replay path and configured-tenant lead-to-signed-estimate-to-job acceptance tests, then expand Bob automation safely. Continue advanced offline synchronization under TKO-0028.
