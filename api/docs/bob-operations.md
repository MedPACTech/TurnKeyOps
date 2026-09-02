# Bob durable operations

Bob conversations and messages use the existing `Chats` and `ChatMessages` Azure Tables. Both entities now record the tenant and actor, and all reads and mutations use the `TENANT={tenant}|USER={actor}` partition. Bob action proposals, approvals, executions, results, failures, and replay keys are stored in the tenant-and-actor partition of the `BobActions` table.

## Service boundary

`BobActionsController` only translates HTTP requests. `BobOperationsService` owns tool lookup, permission checks, confirmation policy, idempotency, persistence, audit events, retry state, and feature-flag enforcement. Providers implement the actual operation behind `IBobActionProvider` and must reuse the same repositories and business rules as the UI.

The initial provider set includes:

- `conversation.read`: safe read action; requires `operations.read` and executes immediately.
- `conversation.archive`: destructive write; requires `operations.manage`, an explicit approval call, and a separate execution call.

Financial, scheduling, customer-facing, and destructive providers always require confirmation. Replaying a completed action or its proposal is idempotent. Failed providers leave a durable `failed` record and can be retried only through the action service.

## Privacy and audit

Inputs, provider results, and model context pass through `BobContextMinimizer`. Authentication material, secrets, connection strings, and common customer contact fields are redacted before storage or model submission. Audit events contain identifiers, tool key, risk, status, and confirmation state; they never include action input or provider output.

## Production flags and emergency disable

Configuration is under `BobOperations`:

- `Enabled` disables all Bob operational action endpoints when `false`.
- `WriteActionsEnabled` preserves reads but rejects every non-read provider when `false`.
- `MaxStoredInputCharacters` bounds minimized action input and result payloads.

Production configuration should set these through environment or managed configuration, for example `BobOperations__Enabled=false` for a full stop or `BobOperations__WriteActionsEnabled=false` to keep read-only assistance. No deployment or data migration is required to activate either kill switch. Existing action rows remain available for audit and can be resumed only after the flag is deliberately restored.

Both flags default to `false` in source-controlled configuration. Enabling operational actions is therefore an explicit production configuration decision; a missing configuration fails closed.

## Development-data migration

The API-side chat workflow is already durable; local `.svelte-kit/local-bob-conversations*.json` files are development-only artifacts and are not production data sources. Before enabling Bob actions for a production tenant, import any approved development transcripts into `Chats` and `ChatMessages` with the correct tenant and actor partition, verify a readback after API restart, then remove the local artifact from the deployment package. Do not copy secrets or customer contact fields from a local transcript.

## Verification

`BobOperationsServiceTests` covers permission denial, explicit confirmation, idempotent replay, provider failure and retry, audit creation, tenant isolation, feature flags, and redaction. A safe read action and a confirmed archive action are the canonical smoke-test pair.
