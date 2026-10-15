# Payroll & Time V1

Payroll & Time is an independent tenant module for field/crew time, earnings classification, payroll preparation and external-provider reconciliation. It does not calculate payroll taxes, execute direct deposits, file returns or administer benefits. This implementation is local; no deployment, live payroll delivery, employee communications or production migration was performed.

## Architecture and module independence

`PayrollDtos.cs`, `PayrollEngine.cs`, `PayrollService.cs`, `PayrollAuthority.cs`, `PayrollStore.cs` and `PayrollIntegrity.cs` form the module. The API is `/api/payroll`; the shared web workspace is `/{bdr|thinkpink|carlzipf}/admin/payroll`. The direct field entry point and install start URL are `/{tenant}/admin/payroll/clock`.

Payroll has a separate `tenant-payroll` blob container and `PayrollVersions` Azure Table (with the existing deployment table prefix). Every write uploads a new immutable tenant-prefixed JSON payload, then conditionally replaces that tenant's current pointer using its exact ETag. Concurrent commands cannot both commit. Failed conditional writes discard the losing blob; ambiguous transport outcomes retain evidence for safe retry. Storage checks preserve approved time, profiles, provider actuals, exports, outbox events and audit history.

Three independent dimensions are represented:

1. **Tenant product availability:** `Enabled`, `JobsEnabled`, `FinanceEnabled`, `SchedulingEnabled` are owner-configured tenant module availability. These are operational configuration, not a new billing/licensing implementation.
2. **Integration choice:** `IntegrateJobs`, `IntegrateFinance`, `IntegrateScheduling` independently govern exchange with available products. Enabling a product does not force an integration or grant a user permission.
3. **User authority:** `payroll.read` / `payroll.write` and action grants determine individual access. People access does not confer compensation access.

Payroll commands never read Finance, Supply or Jobs state when those integrations are unavailable. There is no Finance service/store dependency in the Payroll engine, service, authority or persistence. Optional Jobs and Calendar references are read from tenant-partitioned operational repositories, without requiring Finance or Jobs user permissions just to enter payroll time. Payroll-only operation uses general activity codes and non-Job classifications. Finance's existing external/manual labor workflow is preserved.

### Configuration

A tenant owner enables and configures the module in Setup. Defaults include general/admin/shop/training/travel activity codes, universal labor classifications, leave categories and the generic CSV provider. Overtime thresholds default to **unset**: administrators must enter reviewed policy rather than inheriting an assumed jurisdiction's law. Configuration includes timezone, frequency, workweek start, daily/weekly/double-time thresholds, rolling consecutive-day premium, multipliers, meal-break evidence warnings, event location policy, classifications, provider earnings mappings and optional module exchanges.

Commercial module entitlements are not automatically synchronized with subscription purchases. Integrating this operational module availability with a future product-packaging catalog is a separate platform concern; user grants never substitute for enablement.

## Time model and field workflow

`PayrollTimeEntry` preserves employee identity, work date/timezone, original UTC-offset clock timestamps, duration, breaks, Job/site/schedule references, operational cost code, classification, earnings category, crew, trade/jurisdiction, device/source evidence, source key, location evidence, submission/approval actor/timestamps and correction lineage. Employees are existing iBeam tenant members; no new login or employee identity table is introduced.

Sources are CLOCK, MANUAL, CREW, IMPORT, SYSTEM and CORRECTION. The command workflow uses DRAFT, ACTIVE, SUBMITTED, NEEDS_REVIEW, APPROVED, REJECTED, EXPORTED and CORRECTED. Approved source fields cannot be edited. Rejected time can be resubmitted or replaced through an audited correction.

The direct field screen shows clock status, current Job/activity, a large orange Clock In/Out action, task/Job switch, break control and a note/issue field. Schedule context can suggest a Job; schedules never become worked time. Raw clock overlap and multiple active clocks are rejected. A switch atomically closes the old segment and starts the new segment at the same timestamp. Explicit paid/unpaid/meal breaks preserve timestamps; unpaid duration is subtracted from elapsed time. The optional meal evidence rule raises a review exception, without silently inserting or deducting time.

Elapsed UTC duration is authoritative across daylight-saving changes. Earnings calculations split overnight clock segments at local midnight and apply the configured workweek. Duration-only manual records cannot prove a start/end overlap; their daily total is bounded and their source remains explicit. Clock spans beyond 24 elapsed hours require reviewed manual segments.

Manual time requires a date, duration, activity/classification and reason. Crew entry selects one existing crew/allocation and expands shared hours into individual drafts with per-person overrides. Crews have a lead, existing member IDs, effective dates, trade and qualification metadata. Qualification metadata does not claim an unverified credential.

Location is OFF by default. CAPTURE requests browser permission only at a punch. REQUIRE_WITH_OVERRIDE needs coordinates or an explicit employee note. No background worker tracks location. Radius/geofence evaluation is not implemented.

### Offline behavior

In an already loaded session, clock actions can be saved locally with the original device timestamp and stable command identity. The queue key includes tenant and employee. The UI explicitly displays **Pending sync** and does not represent the punch as server-confirmed. A transport-ambiguous retry uses the same identity; changed command content cannot reuse it. Stale-version/policy conflicts remain visible and saved, including when the tenant disables Payroll, for human review. Further punches are blocked until the pending action is resolved, preventing an invented chain of unconfirmed server states.

There is no cached authenticated-page startup or complete offline multi-punch state machine. Reopening the app requires connectivity and authentication; lists remain available in the loaded screen. Conflicted local evidence requires payroll review and an audited correction. This is a bounded weak-connectivity workflow, not unattended offline payroll.

## Earnings and compensation security

Raw worked time and calculated earnings are separate. Deterministic classification supports configured weekly/daily overtime, daily double time and a rolling seven-consecutive-worked-date premium. Daily premiums are not counted again as weekly regular hours. The original records are unchanged; batches preserve engine/policy/profile/line calculation hashes. Imported time also goes through this classification; direct premium hours are reserved for explicit adjustments to exported earnings.

Hourly profiles contain effective-dated pay rates, provider employee IDs, overtime eligibility, classification, department, employment status/termination context and a **separate loaded labor-cost rate**. Salary-per-period allocation is supported for configured non-exempt=false profiles with approved allocation hours. Non-exempt salary calculation and salary adjustments require provider-specific review rather than an invented hourly equivalent. Salary employees without any approved allocation time block batch preparation and require review; they are never silently omitted.

The action grants are `time.self`, `time.crew`, `time.approve`, `payroll.prepare`, `payroll.export`, `payroll.results`, `payroll.compensation.read` and `payroll.admin`. They are selectable separately in People & access. Staff/Member need explicit Payroll module/action grants. Owners retain full access. Non-owner compensation/admin access requires an explicit permitted override; ordinary People permissions do not suffice. Existing role authority still bounds overrides.

Self-service responses contain only the actor's time; assigned leads see their crews; authorized reviewers see review records. Compensation profiles, batch lines/artifacts, provider actuals and reconciliation amounts require the compensation grant. Readiness and projected earnings-hour reports project only authorized operational fields. No bank account or sensitive tax identifier is stored in the new model. Export reads require both compensation and export grants.

## Submission, exceptions, approval and corrections

Employees/crew leads submit drafts. Missing profile/classification, long durations, late submission, meal evidence and configured prevailing-rate/fringe issues enter NEEDS_REVIEW. Open clocks remain explicit exceptions. Another authorized reviewer approves or rejects the source, with a required reason. Reviewed time can then participate in payroll preparation. Batch preparation fails while any included work date still has active, draft, submitted, rejected or review-needed time.

The audit contains command identity, action, exact content fingerprint, actor, timestamp and reason. Replaying the same committed command is idempotent; different content/actor is rejected. A correction preserves the original and adds a linked draft requiring resubmission/approval. A prepared batch must first be voided before changing its included time. Exported originals remain unchanged: signed adjustments go into a later open payroll period, retain the original exported hourly rate and cannot reverse more hours than were originally exported. Salary adjustment calculations remain a provider review boundary.

## Payroll periods, batches and providers

Periods preserve start/end, pay date, cutoff and lifecycle. They cannot overlap. OPEN/REOPENED/REVIEW periods can prepare payroll. EXPORTED/PROCESSED/CLOSED periods lock source changes. Closing requires exact provider reconciliation; reopening is an explicit payroll-admin action and does not rewrite exported artifacts.

Batches snapshot employee/provider mappings, dated earnings, hours, rates/gross, Job/activity/trade/jurisdiction, loaded cost rates, preparer and calculation evidence. Missing mappings or required compensation prevent export preparation. An unexported batch can be voided and replaced with the next export version; voided snapshots remain. Exports persist the exact artifact and its hash. Download retries return that artifact. Later calculations cannot silently replace it.

`IPayrollProvider` separates export and reconciliation behavior. `GenericCsvPayrollProvider` is the implemented adapter. It emits deterministic quoted UTF-8 CSV, neutralizes formula-like text identifiers and preserves signed numeric adjustments. No credentialed ADP, Paychex, Gusto or QuickBooks API delivery is claimed.

Provider actuals import normalized, tenant-batch-scoped result rows with stable provider run/source identities, earnings type/hours and nullable gross, taxes, deductions, net, benefits, fees and burden. Unknown values stay null. Source retries are idempotent. Wrong hours/gross, missing results and unexpected classifications remain mismatches. Reviewed result corrections append a new source/id with `correctsId`; originals survive. A reconciled processed batch cannot accept rewritten results. Reconciliation compares the current provider result revision against prepared earnings exactly.

## Optional contracts

The durable outbox contains `PayrollTimeApproved`, `PayrollBatchPrepared`, `PayrollActualReceived`, `PayrollBatchProcessed` and `PayrollAvailabilityChanged`. Payroll commits without a subscriber. Finance and Scheduling remain authoritative in their own domains.

**Jobs:** time validates the tenant Job and explicit site/schedule consistency, when the product and exchange are enabled. Operational activity codes are owned in Payroll configuration, so basic time does not depend on Finance tables. Finance consumption requires matching active labor cost-code references; no hidden code or pay-rate substitution occurs.

**Finance:** `PayrollFinanceConsumer` is invoked explicitly through `POST /api/finance/payroll-events`, with Finance owner/control and Payroll compensation grants. The Payroll UI loads Finance context only on demand when that integration is enabled. Prepared batches feed loaded-cost accruals into Finance-owned journals/Job costs. Exact processed provider results produce only the delta from the estimated cost; original estimates remain. Stable batch/stage journal source keys prevent duplicate Job labor posting. Employer actual allocation requires explicit gross, employer tax, benefits and additional burden amounts, including explicit zeros. Missing values are not inferred. Signed-adjustment employer-cost allocation needs reviewed manual Finance reconciliation. Batch versions which were voided cannot feed accounting.

**Scheduling:** deployment `Payroll:SchedulingIntegration:Enabled=true` and the tenant product/exchange settings opt into `IPayrollAvailability`. With deployment opt-in off, ordinary Scheduling never touches Payroll storage. When on, approved leave blocks assignments on the affected tenant-local work dates. Existing Job and locksmith-site-visit scheduling use this contract. Partial-day leave conservatively blocks the entire date. A concurrent leave approval and schedule commit is not a cross-store atomic reservation; this remains a staging/race-hardening boundary.

**PTO:** requests preserve employee/date/hours/category/reason and approval status. Approved paid leave creates a submitted SYSTEM time entry for payroll review; unpaid leave creates availability only. Approved leave is not a fabricated worked shift. Complex accrual balances, benefits administration and opening YTD/PTO-balance migration are not implemented.

## Prevailing wage and certified-payroll foundation

Requirements preserve project/Job, classification, jurisdiction, effective dates, required base/fringe and an authoritative tenant/imported source reference. Time retains classification and work jurisdiction. Missing configured base/fringe evidence appears in review exceptions; no government wage schedule is invented. The dated batch CSV provides employee/project/classification/day-hours/rate/gross source data for future certified payroll. Provider actuals retain deduction/tax/fringe-related amounts separately. This is a reporting/data foundation, not a certified government form or legal-compliance assertion. Union/fringe administration and automatic fringe valuation remain future adapters.

## Bob, reporting and migration

`BobPayrollActionProvider` adds `payroll.readiness` and `payroll.prepare` to the existing policy/audit/action machinery. Readiness returns permission-scoped source records and no compensation. Preparation consumes an explicit command with exact source version and IDs. Its financial action always requires an authorized approval, even when an automation preference says auto. Bob does not invent hours/rates/provider results, apply time corrections, approve payroll time, export payroll or close periods silently.

Reports show visible hours by employee, approved Job/cost code/trade hours, projected premium hours based on recorded time (including drafts), time exceptions, leave status, authorized batch gross summaries and provider reconciliation. Unknown expected workforce coverage is not represented as measured compliance. Schedule-versus-actual analytics, missing-expected-worker detection and historical overtime trend charts need deeper scheduling coverage.

The onboarding foundation accepts normalized time JSON with unique source keys and provider-return JSON. Employee/provider mapping uses secure effective-dated profiles. Data normalization from Acumatica, QuickBooks, provider-specific spreadsheets and legacy exports remains adapter work; no connector is falsely represented as live. Advanced import forms deliberately expose source formats to authorized payroll operators.

## Backward compatibility and rollout

Existing Finance/Finance V1B GL, external/manual labor, costs, periods and exports remain. Existing employee identities, People profiles, Calendar events and Jobs are reused. No historical activity record was rewritten or imported automatically. The existing approved shell, tenant themes, Tailwind and Lucide stack remain. All additions are opt-in tenant configuration; no production records were changed.

Review Payroll action grants, module product/exchange settings, pay profiles, activity codes, earnings mappings, overtime/meal/location policies, provider return semantics and Finance liability/account mappings before rollout. Configure existing Azure Tables/blob storage. Enable the deployment Scheduling bridge only after coordinated staging acceptance. Test CSV acceptance with the chosen payroll provider before processing real pay.

## Verification

Verified locally on 2026-10-14 against working-tree changes based on `9d38595`:

| Check | Actual result |
| --- | --- |
| Full API suite | 600 passed; 13 storage checks skipped in the default run |
| Isolated storage integration suite | All 13 passed against local Azurite, including Payroll CAS/tenant isolation |
| Payroll-specific tests | 40 unit/service cases and one storage case passed |
| Client unit suite | 60 passed |
| `npm run check` | Zero errors and warnings |
| `npm run build` | Passed |
| Payroll Playwright | 16 passed: desktop/Pixel 7, field/crew/approval/export, pending/conflict sync, field restrictions and explicit Bob approval |
| Payroll axe/keyboard | All six workspace views scanned in light/dark on both device profiles; no tested WCAG 2 A/AA or 2.1 AA violations |
| Existing Finance Playwright | 38 passed |
| `git diff --check` | Passed |

API module tests cover Payroll only, Payroll + Jobs, Payroll + Finance, Finance only, and full-platform configuration. Additional service tests assert that disabled Jobs/Calendar integrations are never called, foreign Job partitions are rejected, deployment-disabled Scheduling never touches Payroll storage, and compensation does not enter field/readiness responses. Corrections, original-rate adjustments, provider revision lineage, missing salary allocations and Bob execution guards have dedicated regressions. Browser tests use isolated fixtures, not a live payroll provider or production API. Storage integration uses a temporary local Azurite emulator. Axe, keyboard and mobile checks cover the tested screens; they are not an independent complete WCAG audit.

Commands:

```sh
dotnet test api/TurnKeyOps.Authorization.Tests --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
TKO_STORAGE_TESTS=1 dotnet test api/TurnKeyOps.Authorization.Tests --no-build --filter 'Category=StorageIntegration'
cd client
npm run check
npm run build
node --test --experimental-strip-types tests/*.test.ts
npx playwright test --config playwright.payroll.config.ts
npx playwright test --config playwright.finance.config.ts
```

## Hubbsly and next workstream

Parent: `bab65391-0dd3-45cf-a5fc-246526f6fbef`, **Payroll & Time V1 — Labor Time and Payroll Preparation**. Grouped implementation cards: TKO-0134–TKO-0143. Existing Finance labor cards TKO-0098/TKO-0122 were preserved. TKO-0134–TKO-0143 record verified local implementation. Ready follow-ups TKO-0144 (provider acceptance/rollout), TKO-0145 (Scheduling/PTO concurrency) and TKO-0146 (offline recovery/onboarding adapters) retain the remaining work. The parent remains in flight; production acceptance is not claimed.

Recommended next workstream: **Payroll provider acceptance and controlled rollout** — select the first real provider, validate its earnings/employee/result mappings, reconcile a parallel payroll run, coordinate scheduling/leave race checks and validate tenant-scoped storage/security in staging. Then add provider-specific delivery, richer scheduling coverage, payroll onboarding adapters and deeper offline recovery.
