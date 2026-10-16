# People and Contacts

BDR, Think Pink, and Carl Zipf share these workflows with separate tenant partitions and permissions.

- **People & access** is the employee directory, with job title, configured role and team assignments, and invitations. Roles and Teams are configured in Settings, including module access inherited by employees.
- **Contacts** manages customer/vendor details, a contact address, notes, and links to existing customer records. Linked jobs (including their property addresses) and invoices are shown only when the viewer has the corresponding module permission. Work is matched by customer ID, never by a similar name or phone number.

Both screens use the same company `UserProfile`. Contacts saves preserve employee profiles, verified sign-in identifiers, teams, access roles, and module permissions. A new contact needs an email or international phone number to create/reuse the underlying identity. Creating the contact grants no company membership and sends no invitation. Customer and Vendor profiles are edited in Contacts. Customer/vendor-only contacts do not appear in People & Access. Existing dual-profile records retain their independent contact details when employee access is edited.

Contacts requires `contacts.read`/`contacts.write`; it does not require `users.read`/`users.write`. The legacy `/contact` and navigation `/customers` routes render the same contact workflow for each company.

## Owner removal

An active owner can use **Delete user**, followed by confirmation, to remove another person, including another owner, from the current company. This revokes the tenant membership, releases the seat, and archives the company profile. Existing jobs, invoices, global identity, and other companies' memberships remain intact. Removing your own membership is blocked; another owner must perform that removal. Restoring a profile does not restore membership; a new invitation is required.

The existing Archive action for non-owner administrators continues to exclude owners. Authorization is enforced in the API, including the membership service; hiding a button is not the security boundary.

## Release and validation

Deploy API first, then web. `ContactNotes` is an optional envelope field; existing profiles remain readable and there is no data migration or bulk deletion. Browser coverage includes both company themes, mobile layout, Contacts-only permissions, shared person records, and delete/cancel. API coverage includes cross-tenant lookup, stale edits, permission preservation, and active-owner membership removal.
