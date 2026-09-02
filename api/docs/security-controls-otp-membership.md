# Security Controls for OTP and Membership Changes

## Scope

This document defines the required controls for:

- invite redemption
- verified contact changes
- admin-controlled membership reassignment
- billing-admin boundaries

## Identity Boundaries

- `PlatformUser` is the app-owned global identity record for verified primary contact state.
- `TenantMembership` is tenant-scoped authorization and seat assignment state.
- `UserProfile` is tenant-scoped app profile data and must not be treated as the source of truth for verified login contact.

## Invite Redemption Controls

- Invite redemption requires an exact match against the invited email or invited phone.
- Invite redemption must not allow a user to claim an invite with an alternate verified contact.
- Invite redemption is audit logged as a security event.
- Invite redemption must only transition an invite from `Invited` to `Redeemed` once.
- When billing is enabled, seat assignment happens only after successful redemption.
- When billing is disabled for manual launch provisioning, invites and active memberships use `Unassigned` seat state and do not require a paid subscription.

## Manual Launch Provisioning

- Internal Admin may invite a Customer Admin only through the configured managed-tenant directory; callers cannot supply an arbitrary tenant GUID.
- The platform user-management controller requires the explicit `internal_admin` identity-provider role.
- Customer Admin is the tenant-scoped `admin` role and may manage only the tenant in the validated JWT.
- Controllers delegate invitation and membership business rules to services.
- One-time activation links must be shared through an approved secure channel and become unusable after redemption, cancellation, or expiration.

## Verified Contact Change Controls

- Users may change their own verified login contact only through the verify-new-contact flow.
- Direct edits to `PrimaryEmail` and `PrimaryPhone` on profile update are blocked.
- OTP is sent to the new destination, not the old one.
- The pending request is persisted and tied to the requesting user.
- OTP verification must complete before the new contact is promoted to the verified primary contact.
- After verification, the verified contact is updated on `PlatformUser` and fanned out to tenant-scoped `UserProfile` rows.
- Contact change request and completion are audit logged as security events.

## Admin Reassignment Controls

- Admins may not overwrite another user's verified login contact.
- Reassignment is modeled as:
  1. remove or release the current seat assignment
  2. create a new invite for the replacement contact
  3. wait for the invited user to redeem
- Reassignment is audit logged.

## Billing Admin Boundaries

- Billing admins may change seats, top-up settings, subscription state, and billing configuration through billing-admin APIs.
- Billing admins may not directly alter verified login contact.
- Billing admins may not bypass invite verification rules.
- Billing admins may not mutate ledger, audit, or alert history outside approved corrective flows.

## Operational Controls

- OTP failures, webhook failures, payment failures, top-up failures, renewal failures, and credit-usage failures raise operational alerts.
- Soft-cap threshold crossings raise warning-level alerts.
- All billing and identity-sensitive actions should be traceable through `AuditEvent` and `OperationalAlert` records.

## Implementation References

- `UserVerifiedContactService`
- `InviteService`
- `TenantMembershipService`
- `BillingAdminController`
- `CreditAccountingService`
- `BillingEventWorker`
- `CreditUsageWorker`
