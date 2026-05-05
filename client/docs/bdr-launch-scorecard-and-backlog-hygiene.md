# BDR MVP Launch Scorecard And Backlog Hygiene

Last updated: May 5, 2026

This document defines the minimum operating layer needed to know whether the MVP is working and keep Jira honest during release hardening.

## Launch Scorecard

| Metric | Definition | Owner | Source | Review Cadence |
| --- | --- | --- | --- | --- |
| Intake volume | Count of quote requests submitted or created. | Office owner | Quote request records | Daily during launch week |
| Public-site intake volume | Count of requests where source is `public-site`. | Office owner | Quote request records | Daily during launch week |
| Request aging | Time from `submittedAtUtc` to current time for open requests. | Office admin | Quote request status and timestamps | Daily |
| First-touch time | Time from submission to first operator activity. | Office admin | Quote request timeline | Daily |
| Qualification clearance rate | Percent of requests that move from new/in-review/needs-info to qualified. | Office owner | Quote request status | Twice weekly |
| Time to schedule | Time from qualified to inspection-scheduled. | Office admin | Quote request status and site visit schedule | Twice weekly |
| Time to estimate draft | Time from site visit scheduled or qualified to estimate-drafted. | Office owner | Quote request status and estimate draft saved time | Twice weekly |
| Estimate send rate | Percent of drafted estimates that reach estimate-sent. | Office owner | Estimate draft delivery state | Twice weekly |
| Customer approval rate | Percent of sent estimates that become won. | Owner | Estimate delivery and quote request status | Weekly |
| Invoice send rate | Percent of won estimates that become sent invoices. | Owner | Invoice lifecycle state | Weekly |
| Payment closure | Percent of sent invoices marked paid. | Owner | Invoice lifecycle state | Weekly |

## Launch Health Thresholds

| Signal | Healthy | Needs Attention |
| --- | --- | --- |
| New request aging | Most new requests touched same business day. | Any new request untouched for more than 1 business day. |
| Needs-info aging | Customer follow-up visible within 1 business day. | Needs-info request has no next action or Bob suggestion. |
| Time to schedule | Qualified requests scheduled within 2 business days. | Qualified request waits more than 2 business days. |
| Time to estimate | Visit-ready request drafted within 2 business days. | Site visit outcome exists but no estimate draft. |
| Estimate delivery | Ready-to-send estimates are sent/shared same day. | Ready-to-send estimate stays internal overnight. |
| Invoice handoff | Won estimates create draft invoices immediately. | Won estimate does not appear on invoice tab. |

## Backlog Hygiene Rules

### Card Status

| Status | Rule |
| --- | --- |
| To Do | Card is valid, scoped, and not started. |
| In Progress | Someone is actively working the card now. |
| In Review | Implementation or artifact is complete and has verification notes. |
| Done | Accepted or intentionally closed/superseded with a comment explaining why. |

### Card Comments

Every card moved to In Review should include:
- Changed files or artifact links.
- Verification performed.
- Anything intentionally left unchanged.
- Any follow-up created or named.

Every card closed as superseded should include:
- Which later card or implementation covered it.
- What remains, if anything.
- Why no separate implementation is needed.

### Duplicate And Stale Work

Cards should be cleaned up when:
- A later implementation card already delivered the same behavior.
- The card describes a product decision that is now captured in `docs/`.
- The card asks for a surface that has since moved.
- The card depends on a post-MVP integration, such as email/SMS provider setup.

## Current Backlog Cleanup Decisions

| Card | Decision |
| --- | --- |
| `SCRUM-53` | Finish via workflow owner and handoff contract in `docs/bdr-workflow-owner-and-contract.md`. |
| `SCRUM-54` | Finish via role, status, entity, and source-of-truth contract in `docs/bdr-workflow-owner-and-contract.md`. |
| `SCRUM-55` | Mark as covered by current public quote form to operational request queue behavior, with UAT coverage in `docs/bdr-quote-estimate-uat.md`. |
| `SCRUM-56` | Mark as covered by current estimate creation, customer packet, approval, delivery-link, and invoice handoff behavior, with UAT coverage in `docs/bdr-quote-estimate-uat.md`. |
| `SCRUM-57` | Finish via `docs/bdr-quote-estimate-uat.md`. |
| `SCRUM-58` | Finish via this launch scorecard and backlog hygiene document. |

## Release Review Order

1. Run the UAT happy path.
2. Run the blocked/missing-info path.
3. Check launch scorecard fields are observable from current records.
4. Review Jira for stale To Do cards.
5. Promote only cards with verification notes.
