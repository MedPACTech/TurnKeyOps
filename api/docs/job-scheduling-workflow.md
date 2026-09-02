# Durable job scheduling workflow

TKO-0005 moves released-job scheduling out of `client/.svelte-kit/local-bdr-scheduled-jobs.json` and into tenant-scoped API persistence.

## Storage and isolation

- Job metadata is stored through `IJobRepository` in the authenticated tenant partition.
- Planning, material, checklist, note, and activity history is stored as private JSON in the `job-workflows` blob container. Blob names begin with `{tenantId}/{jobId}` and are referenced only by the job entity.
- Every read and mutation resolves the partition from `IUserContext.TenantId`; the API never accepts a tenant identifier from the client.
- Blob replacement is write-new-then-commit. A failed table write deletes the new blob; a successful write removes the superseded blob on a best-effort basis.

## Service-owned rules

- Creating or scheduling a job requires `InvoiceService.GetJobReleaseAsync` to approve the invoice deposit gate.
- A crew cannot have overlapping `Scheduled` or `InProgress` assignments.
- Supported production transitions are `Scheduled -> InProgress|OnHold|Cancelled`, `InProgress -> OnHold|Completed|Cancelled`, `OnHold -> Scheduled|InProgress|Cancelled`, and `Completed -> Closed`. Pre-release legacy states may move only to `Scheduled` or `Cancelled`.
- Schedule, status, planning/material, and note mutations require the current ETag-backed `expectedVersion` and append actor/timestamp activity.
- A deterministic invoice/job id plus invoice lookup makes release idempotent across repeated submissions.

## HTTP contract

- `GET /api/jobs/paged` and `GET /api/jobs/{id}` provide board and detail reads.
- `POST /api/jobs` releases an eligible invoice into the durable job workflow.
- `PUT /api/jobs/{id}/schedule` validates dates, release eligibility, crew conflicts, and concurrency.
- `PUT /api/jobs/{id}/status` applies the transition matrix.
- `PUT /api/jobs/{id}/planning` updates customer confirmation, milestone dates, materials, and checklist state.
- `POST /api/jobs/{id}/notes` appends an audited note.

Controllers translate HTTP only; rules and persistence orchestration remain in `JobService`.

## Client migration and rollback

The BDR jobs, calendar, dashboard, invoice scheduling, and Bob briefing loaders now use the authenticated API adapter in `bdr-job-scheduling.ts`. The local scheduled-job JSON reader/writer was removed. Existing development-only JSON may be retained as a read-only backup for manual reconciliation, but production does not import it automatically because it has no trusted tenant or concurrency metadata.

Rollback is the predecessor TKO-0004 commit plus the last durable job table/blob backup. Do not restore the local JSON path in production. If a deployment must be rolled back, disable job mutations, restore compatible table/blob data, deploy the predecessor, and reconcile any activity created during the observation window before reopening scheduling.

## Verification

Focused service tests cover release gating, crew conflicts, optimistic concurrency, invalid transitions, tenant-isolated reads, actor/timestamp audit history, materials, notes, and repository failure cleanup. Release validation also includes the full API test suite plus client check/build.
