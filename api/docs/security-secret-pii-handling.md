# Secret, PII, and diagnostic-auth handling

## Production controls

The API no longer contains `TestAuthController`. The `/api/test-auth/*` route family is absent from the compiled controller assembly in every environment, so it cannot expose request authorization headers, claims, token previews, signing configuration, authentication schemes, or runtime token-validation details. `ControllerAuthorizationInventoryTests` enforces both the route's absence and named authorization on every remaining non-public controller action.

The previously tracked root-level `api/user.json` identity export was removed. It was not referenced by runtime or test code, and no replacement fixture is required. The tracked `.azurite` emulator instance file and 9.6 MB debug log were also removed; the log contained two OTP destination records and must not be treated as source or deployment input. Tests that need identities must create deterministic values in the test project, use reserved example domains, and use `555` phone numbers. Production publish inputs and artifacts must never include copied identity exports or local-emulator state.

## Automated scanning

Run the repository scanner from the repository root:

```bash
api/scripts/scan-repository-security.py
```

It enumerates Git-tracked files and fails without printing matched values when it finds:

- private keys or recognized AWS, GitHub, Slack, Stripe, OpenAI, JWT, or Azure account-key formats;
- non-placeholder credential assignments;
- high-confidence email, phone, or street-address fields;
- tracked `.env` files, local secret files, logs, local-emulator state, or root-style `user.json` identity exports.

All API, Node App Service, and Static Web App build templates run the tracked-tree scan before dependencies are restored. They run it again against the assembled publish directory before publishing a deployment artifact. A scan failure is a build failure.

To perform a review of reachable Git patch history locally, run:

```bash
api/scripts/scan-repository-security.py --history
```

History findings are review evidence, not a CI gate: deleting or rewriting already published Git history requires coordinated repository-owner approval and credential-revocation checks.

## Review and remediation decision — TKO-0010

The current-tree scan passes after removing `api/user.json` and the tracked `.azurite` files. The reachable-history review reports the historical identity fixture and local-emulator log. Manual metadata inspection confirmed that `api/user.json` was an identity-shaped record with names and identifiers but was not referenced by code. The Azurite log contained two OTP destination records plus expired challenge hashes generated in May 2026. The configured high-confidence scan found no private key, access token, live provider credential, JWT, Azure account key, or non-placeholder credential assignment in the tracked tree or reachable history.

Decision:

- The identity export is removed from HEAD and therefore from future source and deployment artifacts.
- Local-emulator state and logs are removed from HEAD and ignored so future runs cannot re-add them accidentally.
- No credential rotation is indicated because this review found no reusable credential material. The historical OTP challenges had already expired and their one-way hashes cannot authenticate a caller.
- Published history is not rewritten automatically. A history rewrite would disrupt shared clones and requires repository-owner coordination; access to the repository remains the compensating control for the historical identity-shaped fixture.
- If a future scan confirms credential exposure, stop deployment, revoke or rotate the credential at its provider, update managed configuration, verify the old value is rejected, and only then consider a coordinated history rewrite.

## Contributor rules

- Keep production credentials in the deployment platform's secret store and local credentials in ignored user-secrets storage.
- Never paste authorization headers, tokens, claims, customer contact details, or configuration values into diagnostics, commits, build logs, or card comments.
- Use `example.invalid`, deterministic GUIDs, and `555` numbers in fixtures.
- Diagnostic endpoints must be omitted from production assemblies. A development diagnostic surface, if ever reintroduced, needs an explicit development-only compilation boundary plus tests proving it is absent from Release artifacts.

## Verification evidence

For TKO-0010, the Release publish directory passed the repository/artifact scanner and contained neither an identity JSON fixture nor the former diagnostic controller/route markers. The negative scanner check rejected a synthetic `user.json` artifact and suppressed its matched value. All former `/api/test-auth/*` paths returned the global unauthenticated `401` response with no diagnostic body during a local Release-binary probe; reflection tests independently prove that no matching controller or route exists for an authenticated request to reach.
