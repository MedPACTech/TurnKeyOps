# Operational Dashboards and Alerting Requirements

## Required Alert Classes

- `webhook_failure`
  Trigger when provider webhook parsing, queueing, or background processing fails.
- `payment_failure`
  Trigger when provider invoice or sale payment fails.
- `topup_failure`
  Trigger when automatic top-up charging fails.
- `renewal_failure`
  Trigger when renewal processing throws or cannot complete.
- `credit_usage_failure`
  Trigger when queued credit-usage processing fails.
- `soft_cap_warning`
  Trigger when a user exceeds the configured purchased-credit soft cap for the active usage period.

## Dashboard Views

- Webhook operations
  Show received, queued, processing, processed, failed, and dead-letter trends by provider.
- Billing operations
  Show payment failures, renewal failures, top-up failures, and auto-top-up success/failure rates.
- Credit operations
  Show queued credit-usage requests, failures, soft-cap warnings, and purchased-credit depletion trends.
- Admin security operations
  Show invite redemption failures, contact-change requests, contact-change verifications, and membership reassignments.

## Core Metrics

- webhook success rate
- webhook queue backlog
- billing-event queue backlog
- credit-usage queue backlog
- failed payment count
- failed top-up count
- failed renewal count
- soft-cap warning count
- dead-letter count by queue

## Triage Priorities

- P1
  queue backlog growth, widespread payment failures, renewal failures
- P2
  repeated webhook failures for a provider, repeated top-up failures for a tenant
- P3
  soft-cap warnings and isolated user-contact or invite-related security alerts

## Data Sources

- `WebhookEvents`
- `OperationalAlerts`
- `BillingLedger`
- `CreditLedger`
- `ProcessingCreditUsage`
- `AuditEvent`
- provider dashboards for Stripe and PayPal

## Escalation Rules

- Escalate repeated queue failures after 5 delivery attempts or dead-lettering.
- Escalate payment and renewal failures to billing support on the same business day.
- Escalate repeated soft-cap warnings to tenant billing admins if they continue across usage periods.
- Review worker liveness and queue backlog for both `billing-events` and `credit-usage` as part of daily operations.
