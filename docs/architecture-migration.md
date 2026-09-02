# TurnKeyOps architecture migration

## Target

```text
SvelteKit + Tailwind static applications
                ↓ HTTPS / JSON
          .NET 10 REST API
                ↓
     Services → Mappers → Repositories
                ↓
      Azure Tables / Blobs / Queues
```

The browser applications own presentation and REST calls. Controllers own HTTP concerns only. Business rules live in services, mapping lives in mappers, and persistence lives in repositories.

## Current migration state

### Completed

- Added a browser-safe API client with bearer-token, envelope, error, and API-base handling.
- Synchronized the existing authenticated admin session into the browser token store used by static TurnKeyOps clients.
- Moved BDR estimate-default reads and writes to `GET/PUT /api/admin/estimate-defaults`.
- Removed the `.svelte-kit/local-bdr-estimate-defaults.json` persistence implementation.
- Connected Admin Settings and estimate calculation surfaces to the API-backed defaults.
- Added service-layer validation and tests for estimate defaults.
- Made API CORS origins configuration-driven and included the known local and production client domains.

### Remaining server-owned persistence

- Quote requests and attachments
- Estimate drafts and workflow actions
- Billing settings and invoice workflow state
- Job scheduling and execution state
- Website content
- Contact access overrides
- Bob conversations and operational actions
- Think Pink settings and tenant workflow pages

## Migration sequence

1. Migrate quote requests and attachments to API services/repositories.
2. Migrate estimate drafts, revisions, sending, and approval workflows.
3. Migrate invoices, payment events, reminders, and job-release rules.
4. Migrate jobs, scheduling, materials, notes, and status workflows.
5. Migrate customers/contact access, calendars, website content, and tenant settings.
6. Migrate Bob conversations/actions and all Think Pink pages to the shared tenant-aware APIs.
7. Replace the server action-based OTP bridge with browser auth using API-issued bearer/refresh tokens.
8. Remove all remaining `+page.server.ts`, `+layout.server.ts`, and local filesystem stores from client applications.
9. Switch the client to `@sveltejs/adapter-static`, add the Static Web Apps fallback configuration, and update the deployment pipeline.

The adapter switch is deliberately last: changing it while server routes and actions remain would produce an application that builds incompletely or loses workflows at runtime.
