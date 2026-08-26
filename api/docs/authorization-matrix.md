# TurnKeyOps API authorization matrix

TKO-0006 replaces bare authenticated access with tenant-membership-backed policies. JWT role values cannot self-grant tenant access: the claims transformation removes tenant role claims from the token and adds a tenant role only after loading an active membership from the authenticated tenant partition.

## Identity and tenant rules

- JWTs must pass signature, lifetime, issuer, and audience validation. Clock skew is capped at five minutes.
- Tenant context accepts only a non-empty GUID from the supported tenant claim names.
- Tenant policies require a server-resolved `turnkey_tenant_role` and matching `ClaimTypes.Role` issued by `tenant-membership`.
- Removed, revoked, inactive, deleted, or wrong-tenant memberships grant no tenant role.
- `internal_admin` is preserved only as an explicit identity-provider role. No tenant role implies Internal Admin.
- Repository and service reads use the authenticated tenant partition. Caller-supplied tenant or entity identifiers are rejected or looked up only inside that partition.

## Policy mapping

| Policy | Allowed roles | Intended surface |
| --- | --- | --- |
| `TurnKey.TenantAccess` | Owner, Admin, Billing Admin, Member, Staff, Contact | Session and self/profile reads |
| `TurnKey.TenantStaff` | Owner, Admin, Member, Staff | Quotes, estimates, invoices, jobs, customers, calendar, Bob, weather |
| `TurnKey.TenantAdmin` | Owner, Admin | Membership, roles, invites, activity, tenant and estimate-default mutations |
| `TurnKey.BillingAdmin` | Owner, Admin, Billing Admin | Billing, credits, seats, subscriptions, ledgers, top-ups |
| `TurnKey.InternalAdmin` | Explicit `internal_admin` identity-provider role | Future cross-tenant platform administration only |

All controller actions must declare a named policy or `AllowAnonymous`. `ControllerAuthorizationInventoryTests` enforces that rule by reflection. Public quote and customer-decision endpoints remain anonymous but resolve tenant identity server-side and use signed/idempotent service contracts. Webhooks remain anonymous at HTTP transport and require provider signature verification in their services.

## Permission catalog

The durable role-permission catalog seeds `tenant.read`, `tenant.manage`, `operations.read`, `operations.manage`, `estimate_defaults.read`, `estimate_defaults.manage`, `billing.read`, `billing.manage`, and `membership.manage`. Owner/Admin receive management permissions; Billing Admin receives billing permissions; Member/Staff receive operational permissions; Contact receives tenant read only.

Estimate-default writes enforce `estimate_defaults.manage` in the service in addition to the Tenant Admin endpoint policy. Tenant membership/role and billing mutation services retain their independent membership authorization checks.

## Failure and audit behavior

Authorization failures return the same JSON envelope with `Unauthorized` (401) or `Forbidden` (403), a trace ID, and no claim/token detail. Logs record status, method, path, trace ID, and authenticated state only; they do not include token contents, claim values, or customer data.

## Verification inventory

- Spoofed tenant roles are removed when no active membership exists.
- Membership lookup uses the tenant partition from the validated token.
- Removed memberships grant no access.
- Internal Admin requires the explicit privileged role.
- Wrong issuer and audience tokens are rejected.
- Every production controller action has a named policy or is explicitly anonymous.
- Cross-tenant quote, estimate, invoice, attachment, and job tests verify partition isolation.
