# Invoice payment workflow

## Durable records

Invoice financial headers are stored in the tenant-partitioned `Invoices` Azure Table. Structured line items are stored in `InvoiceLineItems` under `TENANT={tenant}|INVOICE={invoice}`. Delivery metadata, payment/refund events, reminder history, and invoice audit events are stored as JSON blobs in the private `invoice-workflows` container under `{tenant}/{invoice}/{version}.json`.

Every read and authenticated mutation derives the tenant partition from `IUserContext`; no invoice lookup falls back to a cross-tenant identifier scan. Approved quote estimates are synchronized idempotently with the quote request identifier as the invoice identifier. Invoice subtotal, tax, total, amount paid, balance, status, and deposit eligibility are calculated by `InvoiceService`, not accepted from a browser.

The BDR external-admin invoice, dashboard, calendar, and job views use the authenticated invoice API adapter in `client/src/lib/server/bdr-invoices.ts`. Approved estimates are synchronized by `POST /api/invoices/sync-approved-estimates`; list, send, payment, reminder, and job-release values all come from the durable API. The previous `.svelte-kit/local-bdr-invoices.json` store and local estimate-draft reader are no longer used. TKO-0012 can refine the broader external-admin loading and failure-state experience, but it is not responsible for invoice durability.

## Provider configuration

Stripe invoice events use `POST /api/invoices/webhooks/stripe` and require `StripeSettings:WebhookSecret`. Payment objects must include `tenant_id` and `invoice_id` metadata. The event object must expose an amount in cents (`amount_received`, `amount_paid`, or `amount`; refunds use `amount` or `amount_refunded`).

PayPal invoice events use `POST /api/invoices/webhooks/paypal` and require `PayPalSettings:ClientId`, `PayPalSettings:ClientSecret`, and `PayPalSettings:WebhookId`. The provider `custom_id` must contain semicolon-delimited `tenant_id` and `invoice_id` values, and the resource must include its amount.

Both endpoints delegate signature/authenticity verification to the configured `IPaymentProvider` before reading correlation metadata. The idempotency key is `{provider}:{event-id}`. Replayed events return the current invoice without appending a second ledger entry. Failed and pending events remain auditable but do not change collected balance. Refunds can arrive before their corresponding payment; net collected balance is recomputed from the event ledger so later events reconcile deterministically.

## Reconciliation procedure

1. Locate the provider event ID and invoice metadata without copying secrets or raw customer payloads into logs.
2. Read the invoice through its tenant context and compare its `Payments`, `AmountPaid`, `BalanceDue`, and `AuditEvents` with the provider event timeline.
3. Redeliver the signed event when a provider event is absent. Replay protection makes redelivery safe.
4. For an authorized offline correction, call `POST /api/invoices/{id}/payments` or `/refunds` with a unique idempotency key and external reference. Never edit Azure Table or blob data directly.
5. Confirm `GET /api/invoices/{id}/job-release` after reconciliation. A sent, non-void invoice is releasable only when net successful payments meet the configured invoice deposit percentage.

## Rollback and recovery

Code rollback does not require deleting financial records. The table row points to one immutable workflow blob version; successful updates remove the superseded blob only after the table commit. If a table save fails, the newly written blob is deleted. If old-blob cleanup fails after a committed table update, it is a harmless orphan and can be removed by a tenant/invoice path reconciliation job.

To recover from a faulty release, deploy the prior API commit, disable the invoice webhook route at the edge if necessary, and preserve all table rows and blobs. Redeliver verified provider events after the corrected release. Do not reverse payments by deleting events; record a compensating refund/correction through the service.

The client cutover is backward-compatible with the durable API contract but deliberately does not fall back to the former local JSON file. If the API is unavailable, the client request fails visibly and no financial action is reported as successful. Before deployment, discard or archive any development-only `.svelte-kit/local-bdr-invoices.json` file; do not import it automatically because it lacks verified tenant, provider, and audit provenance. If a legitimate development record must be retained, reconcile it as an authorized manual payment or refund with a unique idempotency key and record the source in the note.

## Verification evidence

- `dotnet test TurnKeyOps.Authorization.Tests/TurnKeyOps.Authorization.Tests.csproj --filter 'FullyQualifiedName~InvoiceServiceTests|FullyQualifiedName~InvoiceWebhookServiceTests'`: 8 passed.
- `dotnet test TurnKeyOps.API.sln`: 38 passed, including the job-release integration guard.
- `npm run check` in `client`: 0 errors and 0 warnings.
- `npm run build` in `client`: production SSR/client build passed.
- Focused coverage includes server totals, approved-estimate sync idempotency, partial/full payment, duplicate events, out-of-order refund/payment, refund release revocation, reminder policy, verified webhook dispatch, missing tenant rejection, and tenant isolation.
