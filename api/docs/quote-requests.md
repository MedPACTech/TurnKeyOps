# Quote request persistence

Quote requests are stored in the `QuoteRequests` Azure Table. The tenant GUID is the partition key and the request GUID is the row key. The API is the system of record; the SvelteKit application does not write or read a local quote-request fallback.

## API contracts

### Public intake

`POST /api/public/quote-requests/{tenantSlug}` is anonymous so the public websites can submit requests. The route accepts the public intake fields and attachment metadata. The service resolves `{tenantSlug}` through `QuoteRequestTenants:Tenants`; a submitted tenant ID, status, assignee, timeline, or next action is not accepted as authority.

Supported production slugs are `bdr` and `thinkpink`. Each configured tenant supplies a tenant GUID, default queue owner, and first office action. Production configuration may override these values through the standard ASP.NET Core configuration providers.

The request GUID is an idempotency key. Retrying the same GUID in the same tenant returns the existing durable record rather than creating a duplicate.

### Tenant administration

The following endpoints require an authenticated token with a tenant claim:

- `GET /api/quote-requests`
- `GET /api/quote-requests/{id}`
- `PUT /api/quote-requests/{id}`

Repository reads use the authenticated tenant partition and request row key. The service rejects a body tenant that differs from the authenticated tenant. Controllers contain HTTP translation only; validation, status transitions, tenant resolution, normalization, and activity events are owned by `QuoteRequestService`.

## Status rules

The service validates every status transition. New requests begin in `new`; the normal progression is review or qualification, site visit, estimate, and then won or closed. Invalid jumps such as `new` directly to `won` are rejected before persistence. A needs-information state requires at least one reason, and an inspection-scheduled state requires a valid site visit schedule.

Incoming timeline and reviewer identity values are not trusted. The service creates activity events and stamps the authenticated operator name. Public submission events are stamped as Customer.

## Migration and rollout

1. Back up `.svelte-kit/local-quote-requests.json` if an environment contains development records.
2. Transform each record into the public intake contract or an authenticated administrative import process. Preserve its GUID to make the import idempotent.
3. Submit records to the correct configured tenant and verify them with an authenticated tenant read.
4. Archive the local JSON outside the deployed application after record counts and representative fields match.
5. Deploy the API before the client. The client deliberately fails the submission when the durable API write fails, so an API rollback also requires rolling the client back to a compatible artifact.

Do not copy local seeded/demo requests into production unless the business owner explicitly identifies them as real customer records.

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

For a production-like smoke test, submit one request to each public tenant slug, confirm the returned tenant IDs, then read them with tokens scoped to the matching tenant. A token from the other tenant must not retrieve either record.
