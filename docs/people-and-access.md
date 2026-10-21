# Employees, roles, and company access

**People & Access** is the employee directory for BDR, Think Pink, and Carl Zipf. Search and status filters sit above a full-width table; selecting an employee opens a right-hand details drawer. Employee fields are name, contact details, job title, Roles, and Teams. Creating an employee automatically adds the employee profile. Company, Customer/Vendor classification, customer-account links, and customer portal access belong in **Contacts**, not the employee editor.

An identity can still have both employee and contact records. Editing employee details preserves its existing customer/vendor profile, company, customer link, and verified sign-in credentials. Customer/vendor-only contacts do not appear in People & Access.

## Roles and teams

Open **Settings → Roles & teams** (`/{tenant}/admin/settings/people`). Company owners can create and rename roles, configure module access, and create or rename teams. Changes use a separate, tenant-scoped `employee-access` settings document with conditional versions and audit history. Teams use a table with active employee counts and Edit and Assignments drawers. Assignments can be added or removed from the team drawer or changed on an employee profile. Employees can belong to multiple teams. Adding or removing one membership preserves their other teams. Both Edit and Assignments drawers show current members and support adding and removing members. Renames preserve assignments through the tenant-scoped `teamAliases` map. Team names must be unique. Existing employee team names remain visible after removal from the catalog; new assignments must use configured names.

Roles appear in a table with assigned-employee counts and Edit and Assignments buttons. Edit opens a drawer for the role name and module permissions. Assignments opens a drawer listing employees, with an employee dropdown to add assignments and a Remove action. Employees can also select multiple roles on their People & Access profile; teams are assigned on the profile.

Roles have stable IDs. An employee's `EmployeeRoleIds` references all assigned roles. Existing `EmployeeRoleId` values remain readable until a profile is saved. Effective module access combines the permissions from assigned roles. Changes to a role apply on the employee's next request, with the same effective permissions used for navigation and API enforcement. Membership authority remains a ceiling; an employee role cannot grant staff ownership or access-management authority beyond their membership. Owners retain full access. Only owners assign employee roles. Owners keep full access regardless of configured role assignments; other operators cannot change their own assignments.

The initial **Technician** role enables Ask Bob / Today, Calendar, Jobs, Inventory, Estimates (draft and submit), and Contacts. It grants no Purchasing, Leads, Requests, Invoices, Finance, Payroll, Dashboard, People & Access, Settings, or Billing access. Technician estimates may be drafted, priced, have scope files added, and submitted for office review. Submitting records `SUBMITTED` and an audit event; it does not create a customer proposal link or send a message. Issuing, delivering, voiding, approving, and converting proposals require management authority. Bob applies the same target-module and estimate-management checks.

Ask Bob uses permission-scoped intelligence context for restricted roles. It does not load the legacy aggregate of requests, invoices, and jobs for those users. Today already filters facts, attention, workflows, and actions by the operator's permissions. Dashboard retains its aggregate-module requirements.

Existing employees without a configured role keep their current access policy until explicitly assigned. Existing individual restrictions remain a ceiling on inherited role access. The employee form clears those overrides when an owner explicitly assigns a configured role. Missing role references grant no permissions. Removing the last assigned role leaves explicit empty permissions. New employees may be created without roles and have no module access until assigned a role; an accepted invitation is also required before sign-in; invitations use the staff membership ceiling. No employees are automatically reassigned by deployment.

## Lifecycle and rollout

Archiving removes company membership while retaining the employee record. Restoring the record does not restore membership; create a new invitation. Owner-only deletion and self-removal safeguards remain. Global login credentials continue to use the verified-contact workflow.

Deploy API before Web: the new web app loads the employee role catalog from `/api/people/configuration`. The profile field and settings kind are additive. No existing employee or contact records require a bulk rewrite.

## Verification

Authorization tests cover role inheritance and edits, membership ceilings, explicit restrictions, missing role references, owner preservation, owner-only settings writes, invalid catalogs, tenant scope, contact-field preservation, employee role/team validation, and submission without proposal-management authority. Browser tests cover persisted employee assignments, Settings catalog edits, separation from Contacts, customer portal behavior, keyboard-accessible forms, and accessibility checks. Storage tests needing an explicitly configured Azure/Azurite environment remain separate.
