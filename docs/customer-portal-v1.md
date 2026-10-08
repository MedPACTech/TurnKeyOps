# Customer Portal V1

Implemented locally on 2026-10-07 in the existing TurnKeyOps monorepo. No deployment, production migration, live customer notification, commit, or PR was performed for this work. The existing Inventory/Purchasing changes were preserved; the current repository base is `22d5a8c`.

## Discovery and design

The active application is `client` (SvelteKit), with the existing ASP.NET API and Azure storage. Discovery covered Leads and its intake bridge; immutable Estimate packets, public review links and signatures; Job execution, changes, readiness and completion; Calendar reservations; iBeam OTP and communication transports; Customers, UserProfiles and JobSites; private blob attachments; public website routes; employee permission filters; and the Bob action executor. Hubbsly had no Customer Portal epic or matching customer-access card. Existing ContactAccessGrant assigns field/office/owner roles and is deliberately not reused for customers.

The portal is a projection and authorized-action layer. It does not copy Lead, Estimate, Job, Calendar or customer data into a second operational database. Nine Hubbsly cards group the requested work breakdown; the optional warranty and finance extension points stay on authoritative records.

## Identity and entitlements

- Tenant-neutral iBeam OTP verifies the existing identity. The server sends the API-validated identity/pre-tenant token to `POST /api/portal-identity/{slug}/session`. This exchange requires an authenticated identity, not employee membership or module permissions.
- A separate, random 256-bit opaque token is returned. Only its SHA-256 hash is stored. It expires after eight hours. Each company has a separate HttpOnly, SameSite=Strict cookie, secure in production, scoped to its portal path. Tokens are never included in page data, local storage, URLs or customer links. No employee cookie or role is issued.
- Portal access is disabled by default. An owner grants a current active Customer profile access to its explicitly linked Customer. Each grant is customer-wide, site-specific, or limited to one Lead, Estimate or Job, with an expiry and revocation flag. There is no matching by name, email similarity or phone number.
- Every request reads current access metadata and the authoritative profile directly from the managed profile store. Deleted/inactive/unlinked contacts, expired/revoked grants and sessions, and disabled portals fail closed. Employee permissions cannot substitute for a grant.
- Site grants require both the granted customer and site to match the operational record. A site grant cannot reveal another customer's project at the same site. Leads currently lack an authoritative SiteId and need a customer or explicit Lead grant. Multiple commercial contacts can hold independent grants over the same company/sites/projects.
- Owner administration uses existing tenant authorization, settings permissions and module permissions for staff previews, replies, sharing and notifications. It is separate from customer authorization.

`PortalAccessVersions` holds a conditional tenant pointer into immutable `portal-access` blobs containing access metadata, configuration and preferences only. Compare-and-swap writes prevent stale logins/configuration writes from resurrecting revoked access. Expired sessions are pruned during activation. Large tenants will eventually need indexed grants/session rows rather than a tenant-wide metadata snapshot.

## Screens and workflows

All three configured companies use the same routes under `/{tenant}/portal`:

- Passwordless sign-in at `/login`.
- Home: needs attention, upcoming appointments, active work and explicitly shared recent updates.
- My work: authorized requests, proposals and projects, with a location selector for commercial accounts.
- Messages and Documents: contextual entry points into shared work, with conversation history and file uploads/downloads.
- Account: company contact information, email/SMS opt-in and sign-out.
- Work detail: safe scope/status, proposal review, appointment actions, shared project updates/documents, changes, completion and structured issue reporting.

The shared UI uses Tailwind, semantic colors, Lucide, keyboard-visible focus, labeled forms, typed signatures, large touch targets and light/dark styles. Branding uses company name/contact details, a same-origin logo asset and a bounded accessible accent palette. There are no finance placeholders or internal module dashboards.

## Proposals and signatures

The portal loads the authoritative issued packet and verifies its stored document hash. It projects scope, frozen pricing/options, terms, exclusions, timing, validity and issued attachments using an explicit allowlist. It does not expose the internal DTO, delivery tokens, costs, margins, internal attribution, private notes, calculation rules or revision history.

Accept, decline and request-change use the existing decision pipeline. Signing requires exact revision/hash, a printed name, explicit electronic-signature consent and valid offered options. The signature additionally records the verified portal identity and the customer's comment. The selected total sums frozen option prices; issuing/pricing is never rerun during customer review. A click without signature consent cannot accept a proposal. Concurrent source changes still use the existing Estimate CAS persistence. Existing tokenized public review routes remain compatible, and their historical hash calculation is unchanged.

A Job shows the accepted revision/hash, scope, terms, exclusions, options and signature from its existing immutable accepted snapshot, even if the source Estimate later changes. Frozen accepted attachments can be downloaded through the Job entitlement. An estimate-specific grant is needed to review a separate change proposal.

## Scheduling

Customer-visible Calendar events expose only dates, response state and explicitly offered slots. Staff assignments, private descriptions, resource IDs and other customers' events are excluded. Customers can confirm, decline, request rescheduling or, when enabled, select a contractor-offered alternative.

Every response checks the event version. Selecting a slot writes the same authoritative Calendar event through `CalendarReservationStore`; the shared atomic resource guard revalidates availability. Responses preserve actor/time and a customer note. Declining/requesting a change does not silently cancel or move the appointment. Owners can publish up to twelve future UTC slots using `POST /api/admin/portal/appointments/{id}/offer`.

V1 self-booking means selecting offered windows for an existing eligible appointment. It is not an unrestricted service catalog or a new scheduling engine. The existing legacy Job scheduling boundary remains: older schedule writers outside Calendar need migration for uniform reservation guarantees.

## Job visibility, changes and completion

Canonical Job states map to safe customer labels such as Preparing your project, Scheduled, Work in progress, Waiting on next step, Finalizing your project and Completed. Tenant display overrides change labels only. Private blocker descriptions, readiness lists, tasks, vendor details, costs and employee details never enter the projection.

Activity, evidence and changes have explicit `CustomerVisible` flags, default false. Owners deliberately publish customer-safe updates, select files and release internally reviewed changes through Portal settings. Existing activity does not become visible automatically.

Change decisions bind to the current Job version and a hash of the exact change terms/evidence references. They capture portal identity and decision time. A priced change also requires a separately issued and accepted proposal for the same customer/site and exact revision/hash; the original sold proposal cannot authorize additions. Internal review/pricing policies remain in force.

Completion acceptance is stored in the existing Job execution Acceptances collection, separate from proposal signatures. It requires an eligible operational state, completed gates, exact completion evidence hash, explicit consent and typed signature. Qualified acceptance follows the frozen Job policy. Relevant later changes invalidate the completion revision. Customer-reported issues create structured JobIssue records with portal identity, original Job and service-review type, and invalidate a current acceptance revision.

Warranty foundation: existing Job origin supports warranty/service, and execution now has optional WarrantyEndsAtUtc; service issues retain OriginalJobId. Product/material references and accepted completion dates remain on authoritative execution records. There is no full warranty adjudication or Finance module.

## Communications, files and notifications

Conversations extend the existing ChatMessages repository in a dedicated tenant customer partition. Explicit metadata binds each message/file to its customer and Lead/Estimate/Job context. Internal AI chats and non-visible messages cannot enter the customer projection. A dedicated append operation passes the authorized tenant explicitly, without depending on an employee tenant session. Owners read and reply through Portal settings.

Uploads use private blobs and existing ChatMessage metadata, not a separate document database. JPEG, PNG and PDF are bounded to 10 MB and checked against their content signatures. Downloads reauthorize the record and explicit file reference, validate the tenant/context blob prefix and force attachment delivery with no-store, nosniff and sandbox headers. Job evidence must be explicitly visible; issued/accepted proposal attachments retain their frozen paths. No arbitrary blob URL or path is accepted from a customer.

Notifications reuse iBeam email/SMS, tenant communication profiles and the existing JobNotificationDispatcher/receipt store. Customer opt-in, verified PlatformUser contact, current entitlement, configured template and exact project/proposal version are required. The dispatcher claims one provider attempt; provider acceptance is distinguished from delivery, and uncertain results are not blindly resent. Portal settings exposes explicit owner notification sending. Live outbound transport was not used in tests.

## Customer Bob

Customer Bob is a bounded explanation endpoint over the same authorized projections. It explains shared proposal scope/revision or project status and available next steps. It has no internal Bob context, employee tools, general tenant retrieval, hidden notes or autonomous mutations. Messaging, rescheduling, uploads and issue submission remain explicit customer actions.

This V1 uses deterministic explanations, not a new conversational model or autonomous agent. Tenant-side Bob retains its existing policy executor; new unattended customer outreach and free-form customer chat require a separately scoped policy/provider integration.

## Configuration and integrations

Owners use `/{tenant}/admin/settings/portal` to enable the portal, choose branding/features, preview shared work, publish updates/files/changes, reply and send opted-in notifications. Notification templates and safe Job label overrides also have API configuration support. Configuration saves preserve their current values when edited through the UI.

Carl Zipf’s existing **Customers & contacts** editor also shows portal access for linked customer contacts, with a selector for multiple people and an **Add customer contact** action that preselects the customer. Its new `/carlzipf/admin/contact` profile route uses the same shared Contacts component; contact aliases select the Contacts navigation and keep the workspace scrollable. This legacy editor integration was verified with two additional desktop/mobile browser tests, including creation, grant persistence and axe.

Individual access is managed on a saved Customer contact in **Contacts → select contact → Customer Portal**, also available in **People & Access**. Only owners with settings permission see/manage this section. The contact must be active and linked to a canonical Customer. Enable access, choose all customer work or a named site/project/request/proposal, set an expiry within two years, then save. Repeat to add limited scopes. Site choices come from the customer's existing visible work; new unused sites are not offered yet. The section shows active/expired/revoked state, company enablement, and copyable login instructions; copying does not send an invitation.

Turning access off requires confirmation and atomically revokes all of this contact's tenant portal grants and sessions with the existing compare-and-swap version guard. Other contacts and employee permissions remain unchanged. Individual grant revocation is also available. The shared BFF returns only the chosen contact's grants, enforces same-origin JSON mutations, and rejects stale versions and cross-contact grant IDs; the API retains owner/tenant/relationship authorization. Company settings link to Contacts instead of duplicating grant controls.

Profile-access verification (TKO-0077): 363 API tests passed (seven opt-in storage checks skipped); six existing Contact/People browser regressions and two new desktop/mobile portal-access browser tests passed, including axe, persistence, stale-write and CSRF rejection. The broader People browser run exposed an unrelated pre-existing Estimates assertion expecting “Sent estimates”; it is not counted as passing. Production build and Svelte check passed.

BDR, ThinkPink and Carl Zipf public navigation includes My project. The same tenant portal URL can be linked from an external website; CMS is not required. The manifest and service worker provide an app-like shell. The worker caches no private records and provides an honest offline recovery document; it never queues or reports a successful offline submission.

## Audit and analytics

The existing AuditService receives explicit tenant, verified actor, action and target for activation, proposal views/decisions, appointment responses, messages, uploads, change/completion decisions, service issues, preferences, notifications and administrative grant/sharing changes. Message bodies and file contents are not duplicated in audit metadata. Exact decision evidence stays with the source business record.

These canonical timestamps/actions support future activation, proposal-view/approval, appointment-response, change-decision, message and completion metrics. There is no vanity analytics dashboard. Generic audit and operational record writes are separate transactions; an outbox/reconciliation worker remains a delivery reliability follow-up.

## Migration and compatibility

All schema additions are optional with safe defaults. There is no destructive data migration, renamed source table, bulk history rewrite or automatic grant creation. Existing employee permission grants are unchanged. Portal controllers use dedicated access checks; PortalAdmin maps to existing settings permissions. Legacy public proposal links and signature hashes remain supported. Private existing Job files/activity stay private.

Deploy API and client together. Provision the PortalAccessVersions table and private portal-access/portal-files containers through normal storage infrastructure (or existing development auto-create). Retain immutable source proposal/job blobs referenced by signed evidence. Enable one tenant only after reviewing customer links/grants, communication profiles and policy. Review storage retention, access/session capacity, verified contact lifecycle and transport delivery reconciliation before broad rollout. Rollback by disabling portal configuration; keep additive access/evidence data for audit.

## Verification

Commands from repository root:

```sh
dotnet test api/TurnKeyOps.Authorization.Tests --no-restore -p:UseSharedCompilation=false -m:1
TKO_STORAGE_TESTS=1 dotnet test api/TurnKeyOps.Authorization.Tests --no-build --filter 'Category=StorageIntegration'
npm --prefix client run check
npm --prefix client run build
```

Commands from client:

```sh
node --test --experimental-strip-types tests/*.test.ts
npx playwright test --config playwright.portal.config.ts
npx playwright test --config playwright.estimates.config.ts
npx playwright test --config playwright.jobs.config.ts
```

Latest local results are recorded below and in Hubbsly. API tests cover identity JWT signature/expiry/audience, absence of employee permission inheritance, tenant/customer/site/record isolation, live contact revocation, safe projections/Bob, immutable proposal decisions, change/completion evidence, scoped files/messages, notification opt-in/verification and replay behavior. Storage tests run separately from ordinary unit tests because existing fixtures mutate global key formatting.

Browser tests use an explicit local fixture API and real SvelteKit server/Chromium. They are not live iBeam/provider or production API end-to-end tests. HTTP API tests use TestServer with actual JWT validation and mocked repositories; storage tests separately exercise Azurite. Axe passes apply to tested screens/themes, not an independent full WCAG certification.

| Check | Result |
| --- | --- |
| API authorization/service/HTTP suite | 363 passed; seven opt-in storage checks skipped in default run |
| Isolated Azurite storage suite | Seven passed |
| Client check | Zero errors, zero warnings |
| Client production build | Passed |
| Client unit suite | 54 passed |
| Portal Playwright | 18 passed across desktop and Pixel 7 |
| Portal axe/keyboard | Zero violations on tested Home, Job, Proposal, Login and Account screens; light/dark work views |
| Existing Estimates Playwright | 16 passed |
| Existing Jobs Playwright | 16 passed |
| git diff --check | Passed |

Initial verification found and fixed missing controller authorization registration, a location-selector hydration race and a service-worker scope mismatch. Sandbox restrictions required running .NET and browser/local-storage tests with approved local execution permissions. The initial sandbox build and browser server attempts were not counted as passes.

## Remaining limits and next workstream

- Verify real iBeam OTP/pre-tenant token exchange, enabled-tenant customer onboarding, real API-backed browser flows and authorized email/SMS delivery in staging. No live provider traffic or production activation is claimed.
- Notifications are explicit sends; there is no new unattended reminder/expiration worker, inbound email/SMS conversation reconciliation or provider delivery webhook integration.
- Offered-slot scheduling is implemented; preparing slots uses the API. A richer staff availability picker, unrestricted service self-booking and legacy schedule migration remain separate work.
- Customer Bob is deterministic guidance. Free-form model conversation, photo interpretation and autonomous actions are not claimed.
- One company profile currently links to one canonical Customer. Multiple contacts/sites/projects are supported; one identity representing several unrelated customer entities within the same tenant needs an expanded relationship model.
- File signature checks are not malware scanning. Broad deployment should add quarantine/scanning and retention/quotas according to operational requirements.
- No full warranty module, finance/payments, offline mutation synchronization or independent WCAG audit was built.

Recommended next workstream: **Customer delivery reliability and pilot rollout**—real identity/API/provider acceptance, notification reconciliation/outbox, staff slot-selection workflow and the first reviewed tenant activation. Finance can follow once customer identity and delivery are proven operationally.

## Hubbsly

Epic: Customer Portal V1 — Customer Self-Service Experience (`97a15538-d7a2-436a-ae29-298d88142108`).

- TKO-0068: identity, grants and revocation.
- TKO-0069: portal shell, home, requests and commercial contexts.
- TKO-0070: issued proposal review and signatures.
- TKO-0071: shared Calendar responses and offered-slot booking.
- TKO-0072: visibility, contextual communications and files.
- TKO-0073: change decisions, completion, issues and warranty foundation.
- TKO-0074: customer Bob, configuration, branding and website integration.
- TKO-0075: notification preferences, audit and analytics foundation.
- TKO-0076: authorization, accessibility, storage, regression evidence and rollout verification.
- TKO-0077: contact-profile access controls, scope selection and atomic revocation.

TKO-0068 through TKO-0075 are done for local implementation, with source references, test evidence and limitations recorded on each card. TKO-0076 remains in progress for real staging identity/API/provider acceptance. The epic remains in flight until that rollout verification is complete.

Implementation is local working-tree delivery based on `22d5a8c`. Cards distinguish implemented/tested behavior from staging/operational follow-up; no committed or deployed status is implied. The final storage run also verified real AuditEvents persistence for customer actions without an employee tenant context.


## Residential and commercial customer classification

Customer.customerType is an optional persisted field (`residential` or `commercial`), independent of Lead/Job work type. Legacy records remain unclassified; no heuristic conversion or record migration runs. The API maps the field both ways, validates explicit classification, requires a person/household name for residential and a company name for commercial, and preserves an existing classification when an older client omits it. Customer IDs, work links, sites, contacts and grants are retained when classification changes.

All tenants use the shared CustomerManagement editor and server actions. Carl Zipf's existing Customers route uses it directly; BDR/Think Pink Contacts link to Customer accounts (`/admin/customer-records`). Customer filters include Residential, Commercial and Needs classification; Contacts can filter by the linked customer's type. Commercial lists lead with company names. Residential portal setup prefills the saved customer's details for confirmation, creates/reuses a linked Customer contact, then exposes existing scope/expiry controls. It does not grant employee access, send a message or auto-enable unrestricted portal access. Additional contacts remain supported for both types.

Verification: 369 API tests passed (seven storage tests skipped); five targeted client unit tests passed; customer classification browser checks cover all three tenants, mobile layout, axe, persistence, filters, confirmed residential contact setup and retained contacts when changing type. Existing contacts and portal browser flows remain in the targeted regression run. No production deployment or automatic classification of existing records is performed.
