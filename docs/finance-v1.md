# Finance V1 implementation and handoff

Status: locally implemented and verified financial core; production cutover and the remaining migration/automation scope below are still open. This is tenant business accounting, separate from CreditAccountingService and platform subscription billing. Existing operational data and the pre-existing working-tree changes are preserved.

## Architecture and accounting policy

One shared FinanceService/FinanceLedger serves every tenant and trade. FinanceState contains accounts, monthly periods, immutable posted journals, AR/AP open items and allocations, bills, job costs/budgets, bank activity, reconciliations and audit history. FinanceStore uses tenant-scoped immutable JSON blobs in `tenant-finance` and an Azure Table `FinanceVersions` current pointer. Conditional ETags serialize competing mutations. Old snapshots are retained; an ambiguous persistence failure does not delete potentially committed payloads. This is application-level immutability, not storage-administrator WORM protection. Whole-tenant snapshots impose throughput/size limits that need production load measurement.

Commands require a current expected version, a command ID and a content fingerprint. Source journal fingerprints make identical retries safe and reject conflicting retries. All journals require positive, cent-precision, balanced debit/credit lines in active accounts and an open period. Reversals append compensating entries. Posted source records and history cannot be edited or removed. Reconciled cash dates are locked against backdated postings.

The starter contractor COA includes cash, AR, inventory, AP, tax payable, equity, revenue, direct cost and overhead. Accounts have type, normal balance, optional parent and external ID; used/system types cannot be redefined. USD, calendar-month periods, invoice-based revenue and vendor-bill material actuals are the supported initial policies. Posting mappings are owner-controlled and fixed after the first posting. Inventory receipt and PO events never masquerade as bills or create a second material cost.

## Delivered workflows

- **AR:** explicitly reconcile existing sent invoices and successful payment/refund events into balanced journals and open items. Preserve original invoice identities and payment histories. Allocate receipts across same-customer invoices; partial receipts, refunds and credits are supported. Credits preserve original revenue/tax coding. Finance-origin receipts are projected into existing invoice reads, reminder eligibility and deposit/job-release calculations without writing synthetic events back into source payment history or reposting them.
- **AP:** capture multi-line vendor bills, duplicate-number checks, approval, PO/vendor/receipt/quantity/unit-price matching, strict or documented-warning policy, bill posting, allocations, vendor payments, refunds and credits. Job-coded credits reverse the corresponding cost category. No bank disbursement rails or payroll are introduced.
- **Jobs:** seed budgets from accepted estimate cost snapshots, never selling prices. Separate budget, open PO commitments and posted actuals. Record labor hours/rates, equipment, subcontract and other cost through explicit source IDs; material actuals come from approved vendor bills. Approved changes require the exact signed pricing revision and append contract history. Owner-reviewed completeness and tax basis gate profitability; unknown costs remain unknown. Legacy ambiguous tax/cost snapshots need review. Cancelled already-recorded changes require explicit correction work rather than silent rewriting.
- **Cash:** bank account records contain masked references; CSV activity import deduplicates external IDs. Suggested exact-amount journal matches require confirmation. Reconciliation verifies opening/closing balances, ledger activity and matched statement rows, rejects overlaps, and appends immutable reconciliations.
- **Close:** owner-only close/reopen with reasons. Close first reconciles invoice activity, then checks outstanding drafts/bills, unmatched bank rows and AR/AP controls. Period locks block posting; reopening is audited. External invoice writes and Finance writes are separate transactions, so close still requires an operational source freeze/check during cutover.
- **Reports:** ledger-derived trial balance, income statement, balance sheet, cash position, account activity, customer/vendor aging and control differences, plus job health. Date filters and CSV exports cover trial balance, income and aging. CSV output neutralizes spreadsheet formula injection. Reports never derive truth from UI arithmetic.
- **Bob:** authorized financial workspace reads and unposted journal drafting; drafts go through the existing Bob approval/audit flow and require explicit owner posting. Bob cannot close periods or directly post entries. These are deterministic tools, not a completed natural-language finance-advisor experience.
- **Permissions:** independent `finance.read`/`finance.write` module grants, owner control checks for high-risk actions, live tenant/actor checks and no-store Finance responses. Ordinary staff do not gain Finance through other operational module grants. Portal finance uses live customer/site/job scope, defaults off, exposes only customer invoices/receipts/balances, and never returns vendor costs, margins, GL or bank data. Payment links additionally require a separate flag and trusted HTTPS Stripe hosts.

The existing admin shell and design tokens host eight Finance views: overview, money in, money out, job health, bank/reconciliation, statements, accounting and close. Existing Home/dashboard is not redesigned.

## Migration and compatibility

There is no destructive schema migration or replacement of operational invoices, jobs, estimates, purchasing or portal records. New Finance state is additive and explicit initialization is required. Provision the `tenant-finance` blob container and `FinanceVersions` table with the application's existing storage configuration and access controls. Confirm backups/retention before production use.

Opening-balance import is owner-only and allowed before any other journal. It requires a balanced opening journal, external source IDs, canonical tenant customer/vendor/job references, dated open items and exact AR/AP control reconciliation. COA external identifiers provide an Acumatica mapping foundation. This is **not** a full Acumatica connector or migration wizard: source extraction, active-job cost-to-date, active PO/inventory conversion, repeatable dry-run reporting and production sign-off remain to be built.

Suggested cutover: export and freeze source accounting; agree COA mappings and cutover date; create periods and configuration; import balanced openings and open AR/AP; compare trial balance and aging controls to source; review job budgets, tax basis and completeness; reconcile opening cash; enable staff/portal grants selectively; review duplicate source keys and only then release operations. Do not run historical invoice reconciliation indiscriminately after importing the same AR history. There is no automated historical overlap reconciliation wizard yet. Rollback should disable Finance access and preserve all snapshots/audit rather than delete posted history.

## Verification (local)

- API authorization/service suite: **401 passed, 8 storage tests skipped** in its default run.
- Separate Azurite storage suite: **8 passed, 0 skipped**, including Finance competing posting/close, tenant isolation, stale writes and immutable records. These exercise real Azure Tables with mocked Finance blob payload transport; they are not a full cloud blob integration test.
- Client unit tests: **57 passed**.
- Finance Playwright: **8 passed**, desktop and Pixel 7; all eight views scanned with axe in light/dark themes, keyboard access and overflow checks, unauthorized access and draft persistence.
- Existing Estimates browser regression: **16 passed**. Jobs: **16 passed** in isolated final run. Earlier parallel browser runs exposed shared Vite dependency-cache 504 errors; run these fixture servers sequentially. Portal final isolated run: **17 passed, 1 failed** (desktop message/issue submission). Earlier Portal runs failed different navigation/axe-context assertions. Portal browser validation is not clean and remains a release gap.
- Svelte check: **0 errors, 0 warnings**. Production build succeeds.
- Invariant tests cover randomized balanced journals, unbalanced/invalid journal rejection, closed-period retries, immutable postings, AR/AP control reconciliation, multi-invoice allocation, overpayment rejection, refunds/credits, tax-preserving credits, job-cost duplication, PO matching, unknown budget costs, bank reconciliation, source tenant validation, permissions, Bob and portal privacy.

Browser suites use deterministic fixture APIs; cloud storage, provider webhooks, real payment rails and production data have not been exercised. Existing API nullable/analyzer warnings remain.

## Remaining work / Finance V1B

1. Durable invoice-event outbox/worker and close coordination across invoice and Finance stores. Today operators reconcile invoice activity explicitly and close runs a reconciliation preflight; this is not unattended real-time GL posting.
2. Complete Acumatica migration tooling and historical overlap detection, job cost-to-date/active commitments/inventory import, dry runs and reconciliation sign-off.
3. Production cutover rehearsal, cloud blob integration, backup/restore and load testing, independent accounting review and release approval.
4. Broader cash forecasts, richer vendor bill attachments/disputes/payment scheduling, advanced report filters/comparisons and exports. Current scope is cash position and reconciliation foundation.
5. Approved-change cancellation/correction UI, deeper source drill-through and operational cost capture automation; inventory valuation beyond the selected vendor-bill cost policy remains future work.
6. Bob narrative explanations/communications using the guarded tools; no fabricated forecasts or inferred missing costs. Payroll, fixed assets, advanced WIP and retainage remain deliberately outside this foundation.

Hubbsly epic: Finance V1 — Financial Core & Job Costing (`1d0e7def-27e2-410e-b0ef-325b67a4a773`), with TKO-0079 through TKO-0115. Remaining scope is retained on open cards; local tests are evidence of implementation, not production deployment.
