# Registration and Billing Runbook

## Scope

This runbook covers support procedures for:

- unverified paid accounts
- seat reassignment
- payment failures
- top-up failures
- renewals
- refunds and credit corrections

## Unverified Paid Account

1. Confirm the provider checkout succeeded from `WebhookEvents` and `BillingLedger`.
2. Confirm the tenant billing account and subscription were created.
3. If the account holder has not completed invite redemption or verified-contact flow, do not manually overwrite verified contact.
4. Re-issue the invite or instruct the user to complete the verified contact workflow.
5. Record support actions in the ticket system; keep PHI out of billing records.

## Seat Reassignment

1. Confirm the current membership and seat status.
2. Remove or release the old assignment through the supported reassignment flow.
3. Create a new invite for the replacement contact.
4. Verify that reserved and assigned seat counts reconcile after redemption.
5. Review audit events for `membership_reassigned` and related invite activity.

## Payment Failure

1. Check `OperationalAlerts` for `payment_failure`.
2. Review provider status and `BillingLedger` entries for the failed invoice or sale.
3. Confirm whether the subscription is now `past_due`.
4. Advise the billing admin to update the provider-hosted payment method.
5. Do not collect card details in MedInsights support channels.
6. If the failure originated from queued billing-event processing, inspect the worker status and queue retry history before reprocessing.

## Top-Up Failure

1. Check `OperationalAlerts` for `topup_failure`.
2. Review the tenant billing account for auto-top-up settings and default payment method reference.
3. Review billing ledger entries for `auto_topup.failed`.
4. Advise the tenant to update provider-hosted payment credentials or disable auto top-up if needed.
5. If repeated failures continue, review queue retries and dead-letter status before attempting manual reconciliation.

## Renewal Failure

1. Check `OperationalAlerts` for `renewal_failure`.
2. Review the tenant subscription term window and next-renewal seat count.
3. Review whether purchased credits expired correctly at renewal.
4. Re-run or repair renewal processing only after identifying the root cause.

## Queue Processing Failures

1. Check `OperationalAlerts` for `webhook_failure` or `credit_usage_failure`.
2. Review `WebhookEvents` or `ProcessingCreditUsage` status, retry count, and last error.
3. Confirm the relevant worker is healthy and the Service Bus queue is not backed up or dead-lettering messages.
4. Reprocess only after the parsing, mapping, or downstream dependency issue has been corrected.

## Refunds and Credit Corrections

- Refunds should be initiated in the payment provider.
- Any application-side credit correction must be recorded as an explicit ledger adjustment.
- Do not silently alter tenant balances without an audit trail.

## Contact Change Support

1. Confirm the user initiated a pending `UserContactChangeRequest`.
2. If the OTP expired, instruct the user to start a new request.
3. Do not manually overwrite verified primary email or phone in profile updates.

## Escalation

- Engineering
  queue dead-letter events, repeated webhook failures, data reconciliation issues
- Billing support
  payment failures, top-up failures, renewal anomalies
- Security
  suspicious invite redemption, repeated OTP abuse, unauthorized contact-change attempts
