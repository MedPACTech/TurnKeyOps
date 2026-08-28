# TurnKeyOps release gate and approval contract

## Required pull-request policy

Create the Azure DevOps pipeline from `api/.azure-pipelines/pr-validation.yml` and add its `Required PR validation` stage as a required `main` branch build-validation policy. Disable bypass except for the repository administrators responsible for incident recovery. Require the source branch to be up to date before merge.

The gate deliberately has zero automatic browser-test retries. A failed test is visible, publishes its trace/screenshot/video, and blocks merge. Fix or explicitly quarantine a flaky test in a reviewed change; never mark a failing critical journey as continue-on-error.

| Gate | Evidence | Blocks merge when |
| --- | --- | --- |
| Repository security | Scanner log | a configured secret/PII signature or conflict artifact is tracked |
| Migration posture | Validation log | a relational migration appears without an explicit forward/rollback validation job |
| API | TRX + dependency report | restore, Release build, any test, or vulnerability check fails |
| SvelteKit client | npm audit + check/build/session output | a high advisory, diagnostic, auth-policy test, or build fails |
| Legacy admin | npm audit + check/build output | a high advisory, diagnostic, or build fails |
| Critical E2E | JUnit + Playwright HTML/traces | either brand intake, attachment persistence, authorization negative path, mobile check, or serious accessibility scan fails |

## Controlled production release

Production deploy pipelines accept only annotated tags matching `release-*`; `main` pushes build but do not deploy. Protect release-tag creation to release managers. Configure the Azure DevOps `Production` environment with at least one approver who did not author the change and disallow self-approval.

1. Merge through the required PR policy.
2. Deploy the exact `main` commit to the stable staging environment.
3. Complete the UAT record below and attach links to the validation run and staging evidence.
4. Create an annotated `release-YYYYMMDD.N` tag on that exact commit.
5. Approve the Production environment only after confirming the tag SHA and UAT record.
6. Preserve API and web artifacts with the pipeline run. Deploy both artifacts from the same tag.
7. Run `api/scripts/post-deploy-smoke.sh` with the production API and web origins. Publish its log with the release.

## UAT signoff template

```text
Release tag / commit:
Validation pipeline run:
Staging deployment run:
Tester and UTC time:

[ ] BDR public quote with attachment is visible only in BDR admin.
[ ] Think Pink public quote with attachment is visible only in Think Pink admin.
[ ] Duplicate retry preserves one request and one attachment.
[ ] BDR and Think Pink OTP login complete with the expected role.
[ ] Anonymous API/admin and wrong-tenant access fail closed.
[ ] Internal admin health and tenant views load live data.
[ ] Accessibility/mobile critical E2E passed with zero retry.
[ ] Secrets, dependency, migration/configuration, and artifact scans passed.
[ ] Rollback artifacts and the previous healthy release tag are identified.

Business approver:
Technical approver:
Decision: APPROVE / REJECT
Notes:
```

## Rollback and incident evidence

Keep the previous healthy API and web zip artifacts until the rollback window closes. On a failed smoke check, stop the release, redeploy each previous artifact with `api/scripts/rollback-app-service.sh`, and rerun the smoke script. Record tag SHAs, pipeline run IDs, App Service deployment IDs, UTC start/end times, the failing check, and the rollback smoke log.

Database migrations are not used: durable application state is tenant-partitioned Azure Tables and Blob Storage. Configuration/schema compatibility is therefore enforced by API tests, the production configuration tests, the identity/table bootstrap checks, and emulator-backed E2E. A future relational store must add a forward/rollback migration job to the required gate before its first production schema change.
