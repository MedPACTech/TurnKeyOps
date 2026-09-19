# People and company access

A person has one iBeam identity. Their existing `UserProfiles` row, keyed by company and identity, holds the company's business details and Employee, Customer, and Vendor contact profiles. Profiles can coexist. They do not imply permission to sign in or view company records.

Open **People & access** in BDR or Think Pink admin (`/{tenant}/admin/users`). The former BDR Contacts employee/vendor editor at `/bdr/admin/contact` now opens this persisted directory. Operational customer records remain under `/bdr/admin/customers`; link those records from the person editor. No demo vendor contacts are synthesized. Company names, employee title/team, and business contact details are editable without changing the person's verified login credentials.

## Lifecycle

- Create with a name and one login email or international phone number. Reuse an existing matching iBeam identity; otherwise create an unverified identity. This sends no code and grants no company membership.
- Add any combination of business profiles and optionally link an existing customer belonging to this company.
- Invite the person through the existing activation process. Employee profiles can receive Staff, Member, or Admin roles. Contact is the limited contact role. Contact access does not grant the employee admin portal or a new customer/vendor portal.
- Owners can change access roles and promote an active employee member to Owner. Promotion preserves existing identity roles. Existing owners cannot be demoted or archived through this workflow.
- Archive removes active company membership using the existing seat-release service, then soft-deletes the company profile. Other companies and the global identity remain intact.
- Restore restores the company profile only. A new invitation is required to restore membership. Owners and the current operator cannot be archived.

## Module permissions

Permission choices are **No access**, **View**, and **View and manage**. A null override preserves existing role defaults; an empty override denies all modules. Overrides cannot exceed the role's authority. Owners always retain full access. Profile edits and module restrictions are scoped to the authenticated company; neither target company nor identity can be reassigned through update payloads.

Permissions are read server-side per request and applied to named operational API controllers and external-admin page routes. Navigation uses the same effective grants. Dashboard needs all operational read grants because it aggregates those records. Bob needs all operational read/write grants because it can read and act across modules. Existing role/service checks still apply. Identity/membership/role/invitation mutations through legacy endpoints require an owner; non-owner profile managers cannot assign permissions beyond their own or change another owner.

This release implements company-wide module access, not assigned-record policies for new vendor/customer portal workflows. Existing internal-platform authorization and verified-contact-change flows remain separate. Those future portals must add ownership/assignment checks before exposing transactional data.

## Rollout

Deploy **Production API first**, then **Production Web**. The new web code fails closed if the permissions API is unavailable. The profile fields are additive and existing owner/member rows are included in the directory without a bulk rewrite. Existing employee access roles are presented as employee profiles until saved explicitly. No production people records are migrated or edited by deployment itself.

The application supports shared identity across companies; each company controls only its own profile. Matching a second independent login identity or merging accounts is deliberately not automatic. Changes to login email/phone continue to require the verified-contact workflow.

## Verification

Authorization tests cover role ceilings, empty overrides, owner preservation, cross-company lookup, stale edits, contact/identity separation, archive/revoke, restoration without access, and customer-link scope. An isolated browser/API-contract fixture exercises create, edit, reload persistence, multiple profiles, module selection, archive/restore, owner protections, and accessibility. The fixture is test-only and does not use production storage or send OTPs. Standard release E2E tests run against the local API and Azurite.

Implemented and validated by OpenAI Codex at Christian Armstrong's request.
