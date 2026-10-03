# Production owner seed

One-time, explicitly authorized bootstrap for the BDR and Think Pink production tenants. This console tool is not part of API startup and does not run on deployment.

## Execution

Build the project with .NET 10. Supply a private JSON manifest and the API appsettings.json path. Set `TKO_SEED_STORAGE_CONNECTION` through an authorized environment without putting the connection string in command arguments, source control, or logs.

```sh
dotnet run --project api/TurnKeyOps.ProductionSeed -- /absolute/path/private-manifest.json /absolute/path/api/appsettings.json
```

The default is a plan preview; add `--apply` to write records. The preview identifies matching accounts and planned grants; it is not a complete conflict preflight. Apply operations span multiple tables and are not transactional. If interrupted, resolve the reported conflict and rerun; read-after-write checks verify each completed grant.

The manifest contains `storageAccount` (must be `stturnkeyops`), two `tenants` with `key`, `id`, `name`, `website`, `primaryOwnerKey`, and two `users` with `key`, `name`, either `email` or E.164 `phone`, and `grants` containing `tenantKey` and `internalAdmin`. Tenant IDs must match application configuration. Keep actual personal contact identifiers in the private manifest.

The seed creates unverified identities, tenant profiles, both identity membership directions, and application Owner memberships. Only explicitly selected users receive the `internal_admin` platform role. Existing active access is preserved, removed/inactive application memberships are rejected, and existing records are reused. Role grants send the complete union of existing and desired roles because the identity provider otherwise filters previous assignments out of its write.

No passwords, verification flags, tokens, paid seats, or email/SMS messages are created. Normal OTP verification is required to sign in.

## Production execution record — 2026-09-18

Applied against `stturnkeyops` in `rg-turnkeyops-prod`:

| Account | BDR | Think Pink | TurnKeyOps platform |
| --- | --- | --- | --- |
| Christian Armstrong | Owner | Owner | Internal Admin |
| Robb | Owner | No membership | No platform grant |

Verified through the application membership repository and identity role store. A second apply completed successfully with all record keys and ETags unchanged across AuthUsers, AuthTenants, AuthTenantUsers, AuthUserTenants, TenantMemberships, and TenantProfiles. Final counts: 2 identities, 2 tenants, 3 memberships in each identity direction, 3 application memberships, 2 tenant profiles.

Sign-in remains a separate release dependency: PR #18 fixes anonymous OTP endpoints blocked by the authentication fallback policy. Seeding does not deploy that fix or establish a signed-in session.

## Correction — 2026-09-19

The original verification reused the seed tool's incorrect undashed key format rather than the deployed application's `IBeam:Repositories:AzureTables:GuidKeyFormat` (`D`). It also seeded a plus-prefixed phone while the identity login provider uses digits only. Actual login therefore created a separate identity and personal tenant.

Corrected the tool to match the deployed key configuration and digits-only identity lookup. Repaired the five original application rows into canonical dashed keys, verifying each copy before conditionally deleting its old key. Granted the requested roles to the actual phone-login identity and selected BDR as its default tenant. Passwords and verification flags were unchanged. The earlier unused identity and personal tenant were retained; no account merge or deletion was attempted.

`--repair-seed-keys` is the explicit one-time application-key repair; `--set-default-tenant` selects each manifest user's first granted tenant. Both require `--apply`. Normal reruns do not override an existing default tenant. Existing sessions require a fresh login to acquire the repaired tenant and role claims.
