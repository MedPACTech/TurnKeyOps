# Quote request persistence

Quote requests are stored in the `QuoteRequests` Azure Table. The tenant GUID is the partition key and the request GUID is the row key. The API is the system of record; the SvelteKit application does not write or read a local quote-request fallback.

## API contracts

### Public intake

`POST /api/public/quote-requests/{tenantSlug}` is anonymous so the public websites can submit requests. The route accepts public intake fields only. Attachment metadata, tenant ID, status, assignee, timeline, and next action are not accepted as authority. The service resolves `{tenantSlug}` through `QuoteRequestTenants:Tenants`.

Supported production slugs are `bdr` and `thinkpink`. Each configured tenant supplies a tenant GUID, default queue owner, and first office action. Production configuration may override these values through the standard ASP.NET Core configuration providers.

The request GUID is an idempotency key. Retrying the same GUID in the same tenant returns the existing durable record rather than creating a duplicate.

### Tenant administration

The following endpoints require an authenticated token with a tenant claim:

- `GET /api/quote-requests`
- `GET /api/quote-requests/{id}`
- `PUT /api/quote-requests/{id}`

Repository reads use the authenticated tenant partition and request row key. The service rejects a body tenant that differs from the authenticated tenant. Controllers contain HTTP translation only; validation, status transitions, tenant resolution, normalization, and activity events are owned by `QuoteRequestService`.

## Attachments

Files are stored in the private `quote-request-attachments` Azure Blob container. Blob names use only server-generated identifiers:

```text
{tenant-guid}/{quote-request-guid}/{attachment-guid}
```

The original file name is retained in quote-request metadata for display and downloads, but it is never included in a blob name or public URL. `BlobUrl` remains null. Blob metadata contains the tenant, quote request, and attachment identifiers so storage inventory can be reconciled without exposing customer data.

Public intake uploads files after the quote request has been durably created:

- `POST /api/public/quote-requests/{tenantSlug}/{quoteRequestId}/attachments`

Authenticated tenant users can manage files through:

- `POST /api/quote-requests/{quoteRequestId}/attachments`
- `GET /api/quote-requests/{quoteRequestId}/attachments/{attachmentId}`
- `DELETE /api/quote-requests/{quoteRequestId}/attachments/{attachmentId}`

Upload endpoints accept `multipart/form-data` with one or more `files` fields. The service permits PDF, JPG/JPEG, PNG, WebP, and HEIC/HEIF content, verifies each file's signature against its extension, and ignores the browser-supplied content type. Limits are 10 MiB per file, 10 files and 50 MiB per upload, and 25 files per quote request.

All authenticated reads and deletes resolve the quote request from the caller's tenant partition and verify that its stored blob identity matches the expected tenant/request/attachment path. A batch upload writes blobs first and saves attachment metadata once. Any upload or metadata-save failure triggers best-effort deletion of every blob written by that batch. Deletes remove live metadata before deleting the blob, so a storage failure cannot leave a downloadable record pointing at missing content.

## Status rules

The service validates every status transition. New requests begin in `new`; the normal progression is review or qualification, site visit, estimate, and then won or closed. Invalid jumps such as `new` directly to `won` are rejected before persistence. A needs-information state requires at least one reason, and an inspection-scheduled state requires a valid site visit schedule.

Incoming timeline and reviewer identity values are not trusted. The service creates activity events and stamps the authenticated operator name. Public submission events are stamped as Customer.

## Migration and rollout

1. Back up `.svelte-kit/local-quote-requests.json` if an environment contains development records.
2. Transform each record into the public intake contract or an authenticated administrative import process. Preserve its GUID to make the import idempotent.
3. Submit records to the correct configured tenant and verify them with an authenticated tenant read.
4. Archive the local JSON outside the deployed application after record counts and representative fields match.
5. If `.svelte-kit/blob-storage` contains real customer uploads, match each file to its quote-request attachment metadata, upload it through the authenticated attachment endpoint, and verify an authenticated download before archiving the local copy. Do not copy unmatched files into production.
6. Deploy the API before the client. The client requires both durable quote-request and attachment endpoints, so an API rollback also requires rolling the client back to a compatible artifact.

Do not copy local seeded/demo requests into production unless the business owner explicitly identifies them as real customer records.

Attachment retention follows the quote-request record. Operators should use the authenticated delete endpoint when a file must be removed. Production storage operations should periodically reconcile private-container blobs against live attachment metadata and delete confirmed orphans only after an agreed recovery window. Azure Blob lifecycle rules must not delete live attachment paths independently of quote-request metadata.

## Verification

Run:

```bash
dotnet test TurnKeyOps.API.sln --no-restore
dotnet build TurnKeyOps.API.sln --no-restore
```

From `client/`, run:

```bash
npm run check
npm run build
```

For a production-like smoke test, submit one request to each public tenant slug, upload a representative file, and confirm the attachment response has no public URL. Verify an authenticated download with a token scoped to the matching tenant. Anonymous blob access and a token from the other tenant must not retrieve the file or quote request. Delete the attachment and confirm the authenticated download then returns not found.
