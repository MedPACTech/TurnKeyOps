# Quote estimate workflow

BDR quote estimates are durable API records. The `QuoteEstimate` Azure Table stores tenant-scoped workflow metadata and optimistic-concurrency ETags; the private `quote-estimate-packets` Blob container stores the typed packet and immutable revision snapshots. Blob names contain tenant and request identifiers, never customer names.

## Workflow and API

Authenticated tenant operators use:

- `GET /api/quote-estimates`
- `GET /api/quote-estimates/{quoteRequestId}`
- `PUT /api/quote-estimates/{quoteRequestId}`
- `POST /api/quote-estimates/{quoteRequestId}/send`
- `POST /api/quote-estimates/{quoteRequestId}/revisions`

The service validates the source quote request in the authenticated tenant partition, normalizes location inputs, calculates quantities and prices from tenant defaults, generates customer scope lines, enforces transitions, and records quote-request activity. Updates to existing packets require the current `version`; stale writes are rejected.

Sending creates a random 256-bit capability token, stores only its SHA-256 hash in workflow metadata, and expires it after 30 days. Customer endpoints require the tenant slug, request id, and token:

- `GET /api/public/quote-estimates/{tenantSlug}/{quoteRequestId}?token=...`
- `POST /api/public/quote-estimates/{tenantSlug}/{quoteRequestId}/approve`
- `POST /api/public/quote-estimates/{tenantSlug}/{quoteRequestId}/request-changes`

Decisions are idempotent. A repeated identical decision returns the existing result; a conflicting later decision is rejected. Change requests require a note and open the packet for revision. Approved packets cannot be edited or revised.

## Persistence and failure behavior

Packet blobs are written before table metadata. If the table save fails, the new blob is deleted. After metadata commits, the previous blob is removed; a cleanup failure leaves an unreachable orphan rather than a live record pointing to missing data. Storage reconciliation may delete blobs not referenced by live `QuoteEstimate` rows after the recovery window.

## Migration and rollback

1. Back up `client/.svelte-kit/local-estimate-drafts.json` if it contains real development data.
2. For each record, confirm the source quote request exists in the correct production tenant.
3. Transform its customer/site/notes/location inputs into the authenticated draft contract. Do not import client-calculated totals; the service recalculates them from current tenant defaults.
4. Save the draft, read it back, compare scope and totals, then create/send revisions through normal endpoints where historical state must be retained.
5. Archive the local JSON only after record counts and representative packets match. Never deploy it with the client artifact.
6. Deploy the API before the client. Roll back both artifacts together if the API contract must be reverted; retain the Azure records and blobs for forward recovery.

No secrets, storage credentials, or customer capability tokens belong in source, logs, analytics, or client bundles.
