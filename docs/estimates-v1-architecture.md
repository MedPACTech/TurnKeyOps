# Estimates V1 — architecture and rollout

## Discovery before implementation

Base: local `dev` at `92e5ff3`. The active external-admin application uses `QuoteEstimateService`, immutable private JSON blobs referenced by a tenant-partitioned QuoteEstimate envelope, QuoteRequest intake, signed/tokenized customer review, and downstream invoice synchronization. The older generic EstimateService remains for compatibility. Leads hands off through QuoteEstimate packets. Existing concrete calculations and LocksmithQuotePricing already calculate server-side; locksmith adds policy snapshots and office approval. Job has an existing private workflow payload store suitable for sold-scope snapshots. Public proposal HTML exists; no authoritative PDF-generation service was found. Use accessible HTML plus browser print, without another document service.

Existing per-user module access, persisted role permissions, tenant settings, Bob proposal/approval/execution and iBeam communications are reusable. Hubbsly TKO-0003, TKO-0022, TKO-0027 and TKO-0036 overlap foundations; create an Estimates epic for the common extensions rather than a second trade stack.

## Decisions

Extend the existing QuoteEstimate packet with a common estimate document, confirmed inputs, catalog lines, options, pricing/version snapshot, terms/files, narrative provenance and timeline. Existing packets without the extension retain their current calculation/signature hash. A new shared workspace uses the existing endpoints and new task operations. Legacy concrete/locksmith screens remain compatibility routes. New Lead handoff includes references and known context; text dimensions are suggestions until confirmed.

Trade schemas provide fields and deterministic quantity derivations; tenant settings provide authoritative catalogs/rates/tax/approval thresholds. No default prices, tax guesses or inferred quantities become authoritative. Pricing returns blockers, exceptions and an immutable snapshot. Shared customer output excludes internal costs/margins, policy internals and private notes. Signed content and selected options are revision-bound. Storage must use conditional writes; sent blobs remain available for audit, and stale customer links cannot approve a newer revision.

Do not grant Estimates access through Leads. Check estimates.read/write at service boundaries plus existing module restrictions, including Bob. Sensitive cost access is a separate owner-only projection initially. Keep all existing intake endpoints and data. Configuration is additive under operational settings. No production migration or outbound test messages.

## Rollout

Deploy additive API/contracts first; review tenant catalog and tax/pricing policies explicitly before pricing. Historical packets are not repriced. New fields are optional; signed legacy hashes retain their old algorithm. Reconcile downstream Lead status from the persisted customer decision when retrying a partially completed transition. Preserve all source files and signature evidence; no background cleanup of proposal blobs.

## Implementation map

- `QuoteEstimateService` remains the aggregate service. The workspace/proposal partials add task operations, canonical state, frozen attachments, delivery events and reporting. `QuoteEstimateRepository` uses real Table ETag conditional updates. Sent revision envelopes are archived by token hash before advancing the root; retries preserve newer signature evidence. Historical links are read-only and retain their original expiry. Draft writes cannot mutate issued or signed content.
- `EstimateDocumentDto` adds upstream Lead/customer/site references, confirmed numeric inputs and provenance, scope, timing, terms, exclusions, options, attachments, sales attribution and owner. `EstimatePricingDto` holds deterministic line items, discount, tax, costs/margins, rule fingerprint and approval snapshot. Existing DTO fields and public legacy hashes remain compatible.
- `EstimatePricingEngine` implements concrete area/volume/perimeter and framing area/stud quantities; clearing and doors/locks use confirmed field quantities and catalog rates. Additional required numeric fields extend each trade schema. Materials, labor, equipment, service, subcontract, fee and allowance share the catalog. Full opening/hardware engineering and inventory availability continue through the existing locksmith workflow; no invented availability is added.
- Authoritative sell rates resolve tenant catalog/cost-plus → customer agreement → permitted project override. Explicit tax is required, including zero. No real tenant catalog, tax or rates are seeded. Unconfirmed/missing inputs block issue. Discount, terms/deposit exceptions, missing cost basis, minimum charge, margin and large totals require owner approval. Policy changes invalidate draft pricing; issued revisions retain their snapshot. Alternative groups require exactly one selection; upgrades are additive.
- `/api/estimate-workspace` provides list/detail/configuration, pricing, extraction, office approval, issue/revision/void, file upload, delivery, Bob and Job handoff. The three existing tenant admin routes share one Svelte workspace and pricing configuration screen. Old editors remain under `/admin/estimates/legacy` and old packets redirect there. Stage labels are tenant configurable while state/event keys stay canonical.
- Public proposals share accessible scope/options/terms/deposit/files/signature rendering. Internal preview uses the same component. Typed signature and explicit consent bind revision/hash/selected options, with recipient, time and existing request metadata. Customer and nonowner projections remove costs/margins; public output also removes private notes, qualification and source/owner details. Generic tenant settings cannot expose or overwrite owner-only estimate configuration.
- Source files are retained upstream while drafting and copied by content hash into the existing private packet container at issue. Later source removal does not alter the proposal file. Customer download requires the current or archived revision token. Delivery uses the existing iBeam email/SMS services and tenant sender profile. Issuing a link and confirmed transport delivery are distinct timeline events; failures remain unconfirmed, never silently successful.
- Bob tools: `estimate.summarize`, `estimate.extract`, `estimate.price`, `estimate.revise`, `estimate.issue`, `estimate.remind`. Existing actor-scoped action records, execution/audit and shared tenant action policies are reused. Policies cannot grant user permissions. Initial issue always requires human approval even under auto policy. Extraction records candidate values and source notes without confirming them or supplying prices; users can adopt scope/measurements before calculation.
- Accepted customer decisions synchronize the source request and Lead to Won. Explicit Create/link Job checks Leads, Estimates and Jobs authority, reuses stable identity, and stores accepted revision/hash/signature/selected line items plus scope, commitments, deposit and sales attribution in the existing Job workflow blob. Repeated handoff returns the existing Job; it cannot silently replace sold scope. Invoice fallback uses the accepted total, including selected options.
- Events/state enable volume/value/acceptance/revisions and source/trade attribution through the metrics endpoint. No Home dashboard redesign or accounting/inventory modules were introduced.

## Permissions and runtime behavior

`estimates.read` and `estimates.write` use current per-user module access at the service boundary, including Bob and compatibility controllers. Owner membership initially controls cost visibility and pricing exception approval. Generic settings reads hide the private estimates section from nonowners; generic writes preserve it when editing unrelated settings. Job handoff additionally requires Jobs write access. Tenant IDs come from authenticated context or configured public tenant resolution, never browser-supplied pricing data.

Pricing/saving/issuing are online operations. The installable PWA supports phone/tablet capture, native file/photo picking and device keyboard dictation. Its service worker caches only an offline recovery page, never customer HTML, API responses, sessions or mutation queues. Returning online reloads current authority and data.

## Verification (local dev, October 6, 2026)

- API suite: 265 passed, 1 existing skipped. Covers deterministic trade math, agreements/guardrails, per-user and tenant boundaries, unknown/missing authority, Bob disabled/auto/approval policy, immutable sent/signed history, selected alternatives, frozen attachments, conditional ETags and archive retries, accepted Job scope and generic-settings cost privacy.
- Client unit suite: 49 passed; separate quote-request tenant tests: 2 passed.
- `npm run check`: 0 errors, 0 warnings. Production `npm run build`: passed.
- Estimates Playwright: 14 passed across desktop Chrome and Pixel 7. Both themes, keyboard, option selection, read-only preview, signing, settings, overflow, recovery-only PWA and scoped axe WCAG 2.1 AA checks.
- Leads Playwright regression: 8 passed across desktop/mobile, including themes/axe and offline recovery.
- Public release gates against the real API and isolated Azurite: 7 passed. BDR/Think Pink intake with attachments, tenant/anonymous isolation, mobile public pages and OTP pending/failure/retry behavior. Delivery is disabled in this test configuration.
- No production/demo data creation, real customer sends or cloud deployment. Browser fixtures use explicit nonproduction data; service calculations/signatures are independently tested in the API suite.

Logs for this run: `/tmp/tko-estimates-final-api.log`, `/tmp/tko-estimates-client-tests.log`, `/tmp/tko-estimates-final-build.log`, `/tmp/tko-estimates-browser4.log`, `/tmp/tko-estimates-leads-regression.log`, `/tmp/tko-estimates-public-regression.log`. Browser screenshots/traces are ignored local artifacts under `client/test-results`.

## Deployment checklist and limits

1. Deploy additive API/contracts and client together. No destructive migration is required. Back up existing tables/blobs normally; retained revision blobs now intentionally increase storage use.
2. An owner must configure reviewed catalog rates/costs, tax, terms, approval thresholds and the HTTPS customer origin for each tenant. Confirm per-user Estimates permissions separately from Leads. Existing legacy concrete/locksmith defaults are preserved but are not automatically migrated into the common catalog.
3. Verify sender configuration and deliverability with an explicitly authorized customer/test recipient during rollout. No external transport test was performed here.
4. Retain the existing private packet and source attachment containers; do not apply generic cleanup that removes issued snapshots or archive pointers. Token links preserve original expiration, rather than becoming permanent public signature archives.
5. Bob note extraction is bounded, deterministic pattern extraction, not an unrestricted language-model interpretation pipeline. General semantic drafting, voice transcription services and detailed per-opening engineering are future extensions; native device dictation can supply notes today.
6. PDF output is browser print/save-PDF of the customer snapshot. There is no separate server PDF renderer or stored PDF signature artifact; existing typed-signature evidence is retained in the immutable packet.
7. Delivery reservation and downstream decision retries are durable but not a transactional outbox. A crash after provider acceptance needs delivery-history review before retrying; unattended scheduling/exactly-once delivery is not claimed. Customer decision replay and explicit Job handoff reconcile Won when a prior downstream update failed.
8. Offline editing/sync and formal post-sale change orders are not implemented. A new signed estimate revision does not automatically rewrite an already-created Job. Cost access is owner-only until finer permissions are introduced.

## Hubbsly and next work

Parent: Estimates V1 — AI-First Lead-to-Proposal Workflow (`b8e8301a-fe8e-4e61-a1df-180e48758af9`). Implementation cards TKO-0038 through TKO-0044 cover packets/handoff, schemas/catalog/pricing, shared workspace, Bob, proposals/communications, Jobs/reporting and verification. They extend TKO-0003/0022/0027/0036 rather than replacing those foundations.

Recommended next workstream: accepted-scope Job execution and actual-cost feedback, with explicit change-order approval. Before broad unattended estimate automation, add a provider-idempotent communications outbox/reconciliation worker and a reviewed tenant catalog/delivery rollout. Keep broader Bob semantic drafting and richer trade schemas as separately validated extensions.

Follow-up cards: TKO-0045 covers reviewed tenant rollout and authorized transport validation. TKO-0046 tracks outbox/reconciliation, broader Bob semantic drafting, richer schemas and explicit post-sale change-order work. Parent remains in flight for rollout; the seven local implementation/verification cards are complete.

## User-requested local walkthrough follow-up

On October 6 the user explicitly authorized demo seeding. Six labeled Carl Zipf scenarios were created through the real local API, including signed acceptance/Job linkage and revision history. No production data or communications were touched. See `estimates-demo.md` and `scripts/seed-estimates-demo.py`. Real workflow execution revealed the dependency dropping physical ETags; `VersionedEnvelopeStore` now preserves atomic envelope/metadata reads and write-response ETags for Leads and QuoteEstimates. Web manifest GET/HEAD metadata is publicly accessible without triggering session-cookie cleanup, while admin/API access stays protected. Tracked under TKO-0047.
