# Billing PHI and PCI Boundary Review

## Objective

This review confirms the intended boundary between MedInsights application data and payment-provider data.

## PCI Boundary

- Card and wallet details remain provider-hosted.
- MedInsights stores provider references such as:
  - provider customer id
  - provider subscription id
  - provider payment method reference
  - provider invoice id
  - provider payment intent id
- MedInsights must not store full PAN, CVV, or raw payment instrument details.
- Hosted checkout, provider-managed portals, and provider-managed off-session references keep the application out of direct card-data handling.

## PHI Boundary

- Billing events, ledgers, webhook receipts, and operational alerts must not include clinical data or patient identifiers.
- Billing descriptions should stay limited to tenant, subscription, seat, credit, and provider event information.
- Audit records for identity and billing actions should reference user, tenant, and request identifiers only.
- Support procedures must avoid copying PHI into billing notes, alert context, or provider metadata.

## Current Design Assessment

- Provider metadata includes tenant id, requested-by user id, purchase type, quantity, and price key.
- Webhook payloads are persisted for billing reconciliation and idempotency.
- Provider webhooks are normalized and then processed by queue-backed workers rather than long-running HTTP handlers.
- Billing and credit ledgers record provider and financial event references, not patient context.
- Verified login contact changes are security events and do not alter membership history.

## Risks to Watch

- Raw provider webhook payloads can contain customer-contact details and should be treated as sensitive operational data.
- Alert context should remain billing-specific and avoid arbitrary application payloads.
- Future downstream integrations must preserve the same provider-hosted payment boundary.

## Required Operating Rules

- Do not place PHI in provider metadata.
- Do not place PHI in audit descriptions, ledger descriptions, or operational alert context.
- Prefer internal ids over human-readable user details in logs and alerts.
- Restrict access to billing-admin APIs and operational alert views.
- Treat persisted webhook receipts as sensitive operational data with limited support and engineering access.

## Conclusion

The current architecture is aligned with a provider-hosted payment model and a separated PHI boundary, provided the operating rules above remain enforced during future changes.
