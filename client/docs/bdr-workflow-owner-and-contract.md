# BDR MVP Workflow Owner And Release Contract

Last updated: May 5, 2026

This document freezes the current MVP operating model for the BDR External Admin, customer public site, estimate packet, and invoice packet work. It exists so follow-up implementation does not reinterpret which screen owns each step.

## Primary Surface Ownership

| Stage | Owner | Primary Surface | Handoff Rule |
| --- | --- | --- | --- |
| Public quote submission | Customer | `/bdr/public` | Creates an operational quote request in the External Admin quote queue. |
| Intake triage | Office admin or owner | `/bdr/admin/requests` | Office reviews source details, contact info, files, and qualification readiness. |
| Missing-info follow-up | Office admin or owner, assisted by Bob | `/bdr/admin/requests` | Request stays in the quote queue until missing readiness checks are cleared or explicitly closed. |
| Site visit scheduling | Office admin or owner | `/bdr/admin/requests` and `/bdr/admin/calendar` | Requests move to scheduled inspection once date, time window, site contact, and field resource are saved. |
| Field visit execution | Field resource | Field/mobile path, post-MVP if not available in current slice | Field outcome must feed notes, measurements, photos, and readiness back to the estimate screen. |
| Estimate drafting | Office admin or owner | `/bdr/admin/estimates` | Estimate is created from an operational quote request, not as a standalone object. |
| Estimate delivery | Office admin or owner | `/bdr/admin/estimates` and `/bdr/estimate/[requestId]` | MVP delivery is review-link based. Email/SMS are explicit provider follow-ups. |
| Customer approval | Customer | `/bdr/estimate/[requestId]` | Approval marks the quote request won and makes a draft invoice available. |
| Invoice review and send | Office admin or owner | `/bdr/admin/invoices` and `/bdr/invoice/[invoiceId]` | Approved estimate creates a draft invoice. Sending moves it to active. Payment closes it. |
| Admin settings and website CMS | Owner or admin | `/bdr/admin/settings` and `/bdr/admin/website` | Defaults feed estimate math. Website content feeds the public site. |

## Role Contract

| Role | Purpose | MVP Access |
| --- | --- | --- |
| Customer | Requests work, reviews estimates, approves or requests changes, receives invoice packet. | Public site, estimate packet, invoice packet. No admin shell. |
| Field resource | Completes site visit and captures site outcome data. | Field/mobile path when available. No owner/admin settings. |
| Office admin | Runs daily back-office flow. | Dashboard, quotes, calendar, estimates, contacts, invoices, Bob, settings areas approved for office work. |
| Owner | Full administrative authority. | Everything office admin can access, plus admin role management and owner-only controls. |
| Unauthenticated visitor | Public marketing and intake only. | `/bdr/public` and customer packet links when directly shared. |

Admin access must resolve from session or persisted admin role state. Query-string role bootstrapping is development convenience only and should not be the production authorization model.

## Status Vocabulary

### Quote Request Statuses

| Status | Entry Criteria | Exit Criteria |
| --- | --- | --- |
| `new` | Public or office-created request received. | Office begins review or closes invalid request. |
| `in-review` | Office is actively triaging scope, contact data, files, or routing. | Moves to contacted, needs-info, qualified, or closed. |
| `contacted` | Office has reached out to the customer. | Missing info is resolved, request qualifies, or request closes. |
| `needs-info` | Required qualification data is missing. | Missing readiness checks clear, or request closes. |
| `qualified` | Request has enough context to schedule a site visit or create an estimate. | Site visit is scheduled, estimate is drafted, or request closes. |
| `inspection-scheduled` | Visit date, time window, site contact, and field resource are saved. | Visit outcome supports estimate drafting, visit is rescheduled, or visit is cancelled. |
| `estimate-drafted` | Internal estimate draft exists but has not been sent. | Estimate is sent, revised, or abandoned/closed. |
| `estimate-sent` | Customer review packet is available and sent/shared. | Customer approves, requests changes, or opportunity closes. |
| `won` | Customer approved the estimate. | Draft invoice is reviewed, sent, and moved through billing. |
| `closed` | Request is declined, lost, duplicate, or archived. | Final state unless manually reopened. |

### Estimate Statuses

| Status | Meaning |
| --- | --- |
| `draft` | Internal estimate is being prepared. |
| `ready-to-send` | Estimate is internally ready for customer review. |
| `sent` | Customer review packet is live. |
| `approved` | Customer approved the estimate from the packet. |
| `changes-requested` | Customer asked for a revision from the packet. |

### Invoice Statuses

| Status | Meaning |
| --- | --- |
| `draft` | Invoice exists from an approved estimate and needs office review. |
| `sent` | Invoice packet has been sent/shared and is active for collection. |
| `paid` | Payment was recorded and billing is closed. |

## Entity Contract

### Quote Request

Required for MVP:
- `id`
- `submittedAtUtc`
- `companyName`
- `contactName`
- `email`
- `phone`
- `siteName`
- `serviceAddress`
- `serviceType`
- `propertyType`
- `requestedTimeline`
- `priority`
- `need`
- `attachments`
- `source`
- `status`
- `assignedTo`
- `nextAction`
- `qualification`
- `timeline`

### Site Visit

Required for MVP when scheduled:
- `visitDate`
- `windowStart`
- `windowEnd`
- `siteContact`
- `siteContactPhone`
- `assignedFieldResource`
- `scheduledAtUtc`
- `scheduledBy`
- `notes`

### Estimate

Required for MVP:
- `requestId`
- `revisionNumber`
- `customerName`
- `siteName`
- `serviceSummary`
- `visitFindings`
- `scopeLineItems`
- `assumptions`
- `locations`
- `status`
- `savedAtUtc`
- `sentAtUtc`
- `sentBy`
- `delivery`

Each estimate location represents one project sub-area, such as garage foundation, sidewalk, driveway, patio, slab, or steps.

### Invoice

Required for MVP:
- `invoiceId`
- `invoiceNumber`
- `requestId`
- `customerName`
- `siteName`
- `amount`
- `state`
- `approvedAtUtc`
- `sentAtUtc`
- `paidAtUtc`
- `reviewUrl`
- `basis`

## Source Of Truth

| Data | Source Of Truth |
| --- | --- |
| Customer-submitted request fields | Quote request record. |
| Attachments | Quote request attachment store. |
| Qualification status | Quote request qualification review. |
| Visit schedule | Quote request site visit schedule. |
| Estimate defaults | Admin settings defaults store. |
| Estimate totals | Derived from estimate locations plus current estimate defaults. |
| Customer approval | Estimate delivery state. |
| Invoice lifecycle | Invoice lifecycle record created from approved estimate. |
| Public website content | Website CMS content store. |

## Explicit Post-MVP Rules

- Real email/SMS delivery provider integration is post-MVP unless separately prioritized.
- Field/mobile visit execution can remain scaffolded if office can capture visit outcome manually.
- Multi-tenant production database persistence can replace the current SvelteKit stores behind the same route contracts.
- Scheduling production jobs beyond the quote/estimate/invoice handoff is post-MVP unless scoped separately.
