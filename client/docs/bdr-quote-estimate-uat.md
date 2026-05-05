# BDR MVP Quote-To-Estimate UAT Script

Last updated: May 5, 2026

Use this script to validate the intended MVP path from customer intake through estimate approval and invoice handoff.

## Preconditions

- External client is running.
- Admin access resolves for an owner or office admin session.
- Public site is available at `/bdr/public`.
- External Admin routes are available under `/bdr/admin`.
- The dashboard and left nav are visually locked and should not be reviewed as part of this UAT pass.

## Happy Path

### 1. Customer submits quote request

Actor: Customer
Surface: `/bdr/public`

Steps:
1. Open the public BDR customer site.
2. Complete the quote form with customer name, contact information, site name, service address, service type, property type, timeline, priority, project details, and optional files.
3. Submit the quote request.

Expected result:
- Customer sees the configured success state.
- A quote request is created with source `public-site`.
- Submission metadata and attachments are preserved.
- Request appears in External Admin quote queue.

### 2. Office reviews request

Actor: Office admin or owner
Surface: `/bdr/admin/requests`

Steps:
1. Open Quotes.
2. Select the new request.
3. Review quote details, contact details, files, and qualification checks.
4. Confirm missing-info blockers are either resolved or clearly surfaced.

Expected result:
- The selected quote card uses the approved light-orange selected state.
- Bob suggestions are visible.
- Qualification checks show readiness or needed information.
- Request can move toward site visit scheduling when ready.

### 3. Office schedules site visit

Actor: Office admin or owner
Surface: `/bdr/admin/requests`

Steps:
1. Use the site visit section on the selected request.
2. Enter visit date, start time, end time, site contact, phone, assigned field resource, and notes.
3. Save the site visit.

Expected result:
- Request status becomes `inspection-scheduled`.
- Site visit details are visible on the request.
- Calendar can display scheduled site visit context.
- Timeline/activity captures the scheduling event.

### 4. Office creates estimate

Actor: Office admin or owner
Surface: `/bdr/admin/estimates`

Steps:
1. Open Estimates.
2. Select the quote request from the quote request panel.
3. Add one or more locations, for example Garage foundation and Sidewalk.
4. Enter length, width, depth, waste percent, and number of pours for each location.
5. Review calculated totals.
6. Save draft.

Expected result:
- Estimate is tied to the selected quote request.
- Locations stack cleanly on desktop and mobile widths.
- Results calculate area, cubic yards, forms, rebar, materials, labor, and estimated total.
- Estimate defaults come from Admin settings.
- Quote request status becomes `estimate-drafted`.

### 5. Office sends estimate packet

Actor: Office admin or owner
Surface: `/bdr/admin/estimates`

Steps:
1. Move estimate state to `ready-to-send`.
2. Send/share the customer review link.
3. Open or copy the customer packet link.

Expected result:
- Estimate status becomes `sent`.
- Quote request status becomes `estimate-sent`.
- Customer review link opens `/bdr/estimate/[requestId]`.
- Email/SMS are labeled unavailable unless providers are configured.

### 6. Customer approves estimate

Actor: Customer
Surface: `/bdr/estimate/[requestId]`

Steps:
1. Review estimate packet.
2. Approve the estimate.

Expected result:
- Estimate delivery status becomes `approved`.
- Quote request status becomes `won`.
- Customer sees an approval confirmation.
- Draft invoice is available in Invoices.

### 7. Office sends invoice

Actor: Office admin or owner
Surface: `/bdr/admin/invoices`

Steps:
1. Open Invoices.
2. Select the draft invoice created from the approved estimate.
3. Review approval context, invoice basis, customer details, and Bob suggestions.
4. Submit/send invoice.
5. Open invoice packet.

Expected result:
- Invoice moves from draft to active/sent.
- Invoice packet opens at `/bdr/invoice/[invoiceId]`.
- Invoice card shows amount and concise description.
- Bob suggestions remain available.

### 8. Office records payment

Actor: Office admin or owner
Surface: `/bdr/admin/invoices`

Steps:
1. Select an active invoice.
2. Record payment.

Expected result:
- Invoice status becomes `paid`.
- Paid invoices filter shows the paid record.
- Invoice is removed from active collection work.

## Blocked Path

### Missing site readiness

Actor: Office admin or owner
Surface: `/bdr/admin/requests`

Steps:
1. Open a quote request missing site name or service address clarity.
2. Review qualification checks.
3. Leave site readiness unresolved.

Expected result:
- Site readiness shows needed/missing.
- Site visit section remains blocked or communicates the blocker.
- Bob suggests a useful next move, such as chasing missing intake.
- Request does not quietly advance to estimate/invoice work.

### Customer requests estimate changes

Actor: Customer
Surface: `/bdr/estimate/[requestId]`

Steps:
1. Open a sent estimate packet.
2. Request changes with a note.

Expected result:
- Estimate delivery status becomes `changes-requested`.
- Quote request returns to estimate follow-up work.
- Office can create a revision.
- Previous revision remains available in revision history.

## Regression Checks

- OTP login design and flow remain unchanged.
- Dashboard and left nav remain visually unchanged.
- Bob is present on operational screens where next-action guidance matters.
- Public CMS form submissions land in the same operational queue as office-created requests.
- Approved estimates surface on the invoice tab.
- Admin settings defaults persist and feed estimate totals.
- Website CMS content persists and feeds `/bdr/public`.

## Current Known Launch Follow-Ups

- Replace development query-role bootstrapping with production session identity.
- Wire real email/SMS providers for estimate and invoice delivery.
- Replace local SvelteKit stores with production API/database persistence behind the existing contracts.
- Complete field/mobile visit outcome capture if manual office capture is not enough for launch.
