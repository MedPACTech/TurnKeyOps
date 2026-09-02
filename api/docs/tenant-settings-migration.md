# Durable tenant settings and migration

TKO-0007 moves tenant-operated configuration out of the client process and into Azure Table-backed, tenant-partitioned records. The client no longer writes configuration under `.svelte-kit`, so settings survive application restarts and horizontal scale-out.

## Data inventory

| Previous development store | Durable kind/table | Visibility | API |
| --- | --- | --- | --- |
| `.svelte-kit/local-bdr-site-content.json` | `public-content` / `TenantSettings` | Anonymous read; tenant-admin write | `GET /api/public/tenant-settings/{tenantId}/content`, `PUT /api/admin/tenant-settings/public-content` |
| `.svelte-kit/local-bdr-billing-settings.json` | `billing` / `TenantSettings` | Tenant staff read; tenant-admin write | `GET/PUT /api/admin/tenant-settings/billing` |
| `.svelte-kit/local-thinkpink-settings.json` | `operational` / `TenantSettings` | Tenant staff read; tenant-admin write | `GET/PUT /api/admin/tenant-settings/operational` |
| `.svelte-kit/local-bdr-contact-access.json` | `ContactAccessGrants` | Tenant-admin read/write; owner permission required to grant owner | `GET/PUT /api/admin/contact-access` |
| Packaged brand defaults | `brand` / `TenantSettings` | Tenant staff read; tenant-admin write | `GET/PUT /api/admin/tenant-settings/brand` |
| Estimate defaults | Existing `EstimateDefaults` table | Tenant staff read; tenant-admin write | `GET/PUT /api/admin/estimate-defaults` |

BDR tenant id is `7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b`. Think Pink tenant id is `88888888-8888-8888-8888-888888888882`. Every durable record uses the authenticated tenant partition; callers cannot supply a protected-settings tenant id.

## Concurrency and validation

- Reads return a storage `version`. Updates to an existing document or grant must send that value as `expectedVersion`; missing or stale versions are rejected before persistence.
- Settings use schema version `1`, accept JSON objects only, and cap payloads at 256 KiB.
- Billing deposit percentage is constrained to 0-100. Operational numbers cannot be negative, crew size must be at least one, and string lists cannot contain blank entries.
- Keys that look like passwords, tokens, credentials, private keys, API keys, or access keys are rejected from settings values.
- Protected integrations store only `secret://`, `keyvault://`, or `env://` references. Reads expose configured key names, never reference locations or secret material. Public content cannot have secret references.
- Settings and grant mutations produce audit events without payloads, secret references, or raw contact ids.

## Development migration

1. Preserve the old local JSON files until the target tenant has been verified. They are ignored by the new runtime but can be used as migration input.
2. Sign in as an authorized tenant administrator and select the correct tenant. Never reuse a token issued for another tenant.
3. For BDR public content, place the old JSON object in the `values` property of a `PUT /api/admin/tenant-settings/public-content` request. For BDR billing, use `billing`; for Think Pink, use `operational`.
4. Use `schemaVersion: 1`, an empty `secretReferences` object unless approved reference URIs are required, and `expectedVersion: null` when the durable document does not yet exist. If it exists, read it first and provide its returned version.
5. Convert the contact access object into one `PUT /api/admin/contact-access/{contactId}` request per entry. Use the current grant version when replacing an existing grant.
6. Read the settings back through the same API and verify the tenant, normalized values, version, and public/protected projection before deleting the old development files.

Packaged defaults remain the zero-data baseline. An administrator's first save creates the durable record, which supports clean development environments without copying local files. Production migration should be performed once per tenant and captured in deployment evidence; do not commit migrated JSON or real secret values.
