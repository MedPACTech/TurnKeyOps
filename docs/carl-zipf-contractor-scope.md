# Carl Zipf: doors and locksmith contractor scope

Confirmed product scope from the October 2, 2026 discovery conversation. This is a design specification, not a statement that these capabilities have been implemented.

## Platform fit

Add a reusable doors-and-locksmith trade profile to TurnKeyOps, with Carl Zipf Lock Shop as a tenant. Extend the existing public site, staff/admin, customers, site visits, estimates, invoices, scheduling, and AI modules. Do not create a separate application stack for this contractor.

Existing foundations include `client/src/lib/config/tenants.ts`, the BDR and Think Pink route families, `api/docs/quote-estimates.md`, and `api/docs/invoice-payment-workflow.md`. Some existing adapters and estimate contracts are BDR-specific; verify and generalize them before reuse. Existing approval records are not evidence of a complete signature implementation.

## Job types and staff capabilities

Residential and Commercial are job types, not operating divisions. Keep one shared calendar and staffing pool. Each tech can be enabled for Residential, Commercial, or both in admin staff settings.

A job's type determines eligible techs, quoting workflow, measurement fields, and hardware sets. Enforce eligibility in assignment and quote APIs as well as the UI. A tech enabled for both uses the workflow for the current job. Keep job type on the job rather than inferring it solely from the customer; one customer may have both kinds of work.

Initial services: door/frame replacement, lock and hardware installation, repairs, and rekeying. Emergency lockouts and electronic access control are not confirmed initial scope.

## Field experience

Provide an installable phone/tablet PWA. The intended connected flow is:

1. Open an assigned job or start an authorized field request; identify customer, site, and job type.
2. Record named openings, measurements, photos, existing hardware, and desired work using forms or AI-assisted dictation.
3. Once required inputs are complete, generate the quote immediately using applicable catalog items and admin pricing rules.
4. Let the tech review the quote and either email a customer link or hand over the device for customer review and signature.
5. Capture quote approval as a distinct event tied to the exact quote revision.
6. Offer eligible installation slots on the shared calendar and confirm the booking after any configured approval/payment conditions are met.
7. At completion, obtain a separate acceptance signature on the invoice/completion record and support payment when configured.

Quote approval and completion acceptance must remain distinct even when a small job completes during the same visit. Preserve the signed document revision, signer details, timestamps, and audit history. Changes after signing require an explicit revision/approval flow.

## Admin configuration and AI quoting

Admin owns required inputs, residential/commercial hardware availability, labor pricing models, product prices, customer discounts, discount limits, margin/amount thresholds, and approval requirements. These are configurable policies, not fixed assumptions about Carl Zipf's current pricing.

AI structures field observations, identifies missing inputs, suggests compatible products, and assembles a customer-readable scope. The pricing service calculates authoritative totals and applies customer discounts and guardrails. Quotes within policy are ready for tech review and immediate presentation; exceptions explain what needs office approval. Do not invent measurements, prices, availability, or compatibility.

Persist the applicable pricing/rule version with a quote so subsequent admin changes do not silently alter issued documents. Missing required data should produce a specific prompt, not a fabricated completed quote.

## Opening records and starter catalog

Model customers with multiple properties/sites and multiple named openings per site from the start. Preserve opening-level products, labor, photos, and measurements in quote scope.

Proposed starting measurements, conditional on the work and product: door slab width/height/thickness; frame/unit and accessible rough-opening width/height; jamb depth; handing and swing with a reference diagram; backset; bore sizes and spacing; hinge and strike locations; threshold/clearance observations. Distinguish measured, estimated, and unknown values. Commercial assessment can additionally record pairs, active leaf, closer/exit hardware, and observed rating labels. Product-specific templates and tech verification govern final fit; the form does not certify code compliance.

Manufacturer references for further template design:

- https://www.thermatru.com/technical/technical-manuals/
- https://commercial.schlage.com/en/resources/installation-setup/templates.html

Seed a small illustrative catalog of residential and commercial doors, frames, locksets, deadbolts, hinges, closers, exit hardware, and service/labor lines. Mark sample prices and catalog data clearly. Give products stable internal IDs and optional external identifiers for later mapping to Carl Zipf's existing database; live import and inventory integration are deferred.

## Public website and appointment booking

Refresh the existing brand/site at https://carlzipflockshop.com/ using its logo and contact information. Provide residential and commercial service paths, structured request details, photo uploads, and appointment selection.

Public appointments default to assessment/service visits; do not promise an installation date before scope, duration, and product availability are known. Offer slots based on shared-calendar availability and eligible staff without exposing staff calendars or customer information. Recheck availability atomically when confirming a booking.

Admin can disable self-booking and return the site to request-and-callback mode. This fallback must preserve request details and uploaded photos.

## Offline behavior

Offline support is desired for the PWA. Proposed baseline: cached authorized job data and catalog/rules, local measurements/notes/photos, queued synchronization, explicit pending state, and conflict handling. Keep locally stored customer data scoped to the authenticated user and tenant.

An offline draft may use cached rules with a visible freshness status. Cloud AI, email delivery, live availability, and payment processing require connectivity. Never display a queued email, booking, or payment as confirmed.

Offline signatures remain a design decision: if included, capture the exact immutable document and signature locally, then synchronize idempotently with visible pending status. Do not silently reprice or rewrite signed content during synchronization. Offline signing was not separately confirmed by the user.

## Office scope and payments

Initial office scope: customers, sites/openings, jobs, shared scheduling and staff assignment, quotes, signatures, invoices, catalog management, and pricing/permission configuration. Advanced dispatch, purchasing, supplier lead-time integration, and truck inventory are deferred.

Plan the payment workflow for deposits, payment at completion, and commercial payment terms. Provider/accounting choices and Carl Zipf's actual collection policies remain pending business input. Reuse existing invoice/payment foundations where appropriate; do not activate a live provider or invent payment terms.

## Implementation boundaries

Start with trade/tenant configuration, job-type eligibility, and opening/catalog contracts, then extend the shared quote and pricing workflow through field capture, signing, and scheduling. Public intake and booking should feed those same records. PWA synchronization and payments must preserve authoritative quote, booking, and invoice state.

Verify tenant isolation, residential/commercial assignment eligibility, pricing guardrails, signed-revision integrity, concurrent booking behavior, and retry/synchronization idempotency as these capabilities are implemented. Any task cards belong in Hubbsly MCP, per workspace instructions.
