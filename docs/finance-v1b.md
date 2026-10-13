# Finance V1B — Construction accounting

## Architecture and compatibility

Finance V1B extends the existing tenant Finance aggregate, immutable payload/version pointer, ledger, subledgers, financial permissions, periods and audit. It preserves the conventional Finance workspace and existing invoice, AP, GL, bank and report endpoints. Construction billing credits a configurable billing liability; earned revenue is recognized separately through approved WIP adjustments. Account IDs are tenant mappings, not hard-coded construction assumptions.

The BDR, ThinkPink and Carl Zipf Finance workspaces link to Construction finance and owner-only Labor, accruals & close operations. Both retain the existing app shell and light/dark themes. Report tables have captions, column/row headers and keyboard-focusable overflow regions. Print and downloadable CSV reports expose authoritative backend results; exports neutralize spreadsheet formula text, including formulas hidden behind whitespace, control characters or BOM.

## Contract, schedule of values and progress billing

Original contract and approved change values reconcile to accepted Job sources. Draft/pending changes do not become revenue. Immutable contract revisions preserve approved changes and SOV history. SOV totals reconcile to the current contract. Applications move from draft through review/approval to invoiced; unissued applications can be voided. Issued application history is immutable.

Application lines distinguish new completed work, new stored materials and installation from previously billed stored materials. New stored materials require supporting document references. New work plus newly stored material is billable; installing previously billed stored material transfers it to completed work without billing it again. A pure transfer can be recorded without inventing an AR document or zero-value GL journal. Server reports preserve completed/stored amounts, prior/current/total billing, line retainage and balance to finish.

AR retainage uses a dedicated receivable account, separate from ordinary AR. Authorized partial/final releases create audited customer AR without recording revenue again. Duplicate commands, overbilling, excess retainage release and closed-period posting are rejected.

## Forecasting, cost codes and WIP

Job health distinguishes contract, budget, actual costs, relieved remaining commitments, remaining forecast and EAC. PO bill quantities and linked imported commitments relieve remaining commitments rather than adding the original PO value to actual costs. Reviewed forecasts carry evidence and optional category/cost-code detail. Missing, stale or incomplete inputs remain explicit exceptions and unknown results.

Cost-code budget allocation is a draft/review workflow. Reviewed allocations reconcile exactly to known category budgets and preserve their category basis. Unknown category budgets are not fabricated. Code reports display budget, remaining commitment, actual, forecast, EAC and variance, with current/stale allocation and unclassified-source indicators. Commitment coding records the reviewed Job, approved PO line or imported commitment and cost code. Category-only forecasts do not invent code-level forecasts. Source drilldowns preserve transaction and journal IDs.

Cost-to-cost and completed-contract recognition are supported. Earned revenue remains independent of billing. WIP drafts preserve source/calculation evidence, exceptions and account mappings; explicit owner approval precedes posting. Posted period snapshots and linked balanced journals are immutable. Underbilling is a contract asset; overbilling is a contract liability. Backlog definitions explicitly distinguish contract less earned revenue from contract less billed.

CSV exports cover Job health, category and code reports, billing, retainage and selected immutable WIP snapshots. Job/trade filters apply to the exported report.

## Construction checkout and customer portal

The existing customer invoice portal projects only grant-scoped construction AR and receipts. Customers can review progress application lines, retainage and net due, then acknowledge with a name and explicit consent. Customer acknowledgment is separate from internal accounting approval and does not post the ledger.

Finance owners can dispatch a Stripe card checkout for the current construction AR balance, retry its original persisted intent, expire it before changing AR, and retry pending verified receipt posting. Intent identity, customer, Job, AR source, amount, currency, cash/clearing account and merchant account are immutable. A durable intent commits before the external create call; provider create and expiry use stable idempotency keys. Ambiguous external outcomes remain pending for recovery, rather than creating another checkout. Manual allocations against active checkouts require resolution first.

The provider is disabled unless deployment explicitly configures all of:

- `ConstructionPayments:Tenants:{tenant-guid}:Enabled=true`
- `ConstructionPayments:Tenants:{tenant-guid}:StripeAccountId=acct_...`
- Existing SDK `StripeSettings:SecretKey` and dedicated `ConstructionPayments:WebhookSecret`
- `ConstructionPayments:ReturnUrl` as a trusted HTTPS URL

Checkouts execute on that tenant's connected merchant account, not the platform subscription account. No live merchant or credential configuration is inferred. The dedicated endpoint signing secret never falls back to the platform SaaS webhook secret, so billing and construction receipts can coexist. The signed endpoint is `POST /api/finance/construction/payments/stripe-webhook`. Only paid `checkout.session.completed` card sessions are accepted. Verified tenant/account/session/customer/AR metadata, amount and currency must match the persisted intent. Distinct provider event IDs for the same checkout session cannot settle twice.

Verified receipts are durably recorded before ledger posting. Posting debits the selected receiving cash/processor-clearing account and credits AR; it does not recognize revenue again. Closed periods, receipts predating the AR document and conflicting remaining balances preserve pending evidence for accounting review. Future-dated AR cannot be dispatched early. The portal exposes a ready payment URL only when tenant payment presentation is enabled, grants permit the source Job and the checkout amount still matches current AR.

Provider refund webhooks are intentionally rejected; audited manual Finance refunds remain the refund foundation. Processor fees are separate reviewed accounting/reconciliation entries, not inferred from the gross checkout receipt. A test checkout is not evidence of configured live payment processing.

## Labor, accruals and close

Time can be entered or imported from canonical tenant activity records with hour units. Since those activity records lack a Job dimension, the owner maps the Job explicitly. Employee/Job tenancy, source duplicates and daily hours are validated. Approval snapshots base/burden cost rates and explicit regular/overtime/double-time classification; posting is a distinct action that debits Job labor and credits a dedicated accrued liability. Owner-only payroll CSV exports preserve posted time/classification and rate evidence without executing payroll or calculating wage taxes.

Accruals are drafted and explicitly posted, with linked reversal dates/journals. Recurring templates require explicit monthly posting. Configurable required/warning checklist items, unapproved time, WIP exceptions and accruals augment close blockers. Period close remains an authorized human action.

Scheduled reversals require both deployment `Finance:AccrualReversals:Enabled=true` and owner `reversal-policy` opt-in. Defaults are disabled. The worker scans every 15 minutes, honors original reversal dates and records immutable attempt/audit evidence. Closed periods produce visible blocked attempts rather than shifted dates; a later UTC day retries after resolution. CAS conflicts remain pending. The worker does not approve/post new drafts.

## AP retainage, banking, assets and compliance

Retained vendor bills preserve gross cost/commitment relief, with net ordinary AP and a separate retained-payable liability. An owner configures a dedicated AP retainage account. Release creates a separate payable without another cost; a retained-bill gross credit reduces held amounts and unpaid AP while reversing original cost dimensions. Excess release, duplicate release and credits against already-paid balances are rejected. Existing net/release payable settlement feeds actual vendor payment evidence.

Bank ingestion supports normalized CSV and OFX/QFX, reviewed split matches and processor batches. OFX/QFX requires one account, USD currency and complete transaction identities; external entities are rejected. Provider transactions share stable ingestion identities to prevent duplicate imports.

The executable normalized bank gateway requires deployment-owned `Finance:BankFeeds:Tenants:{tenant-guid}:Enabled`, `ProviderId`, `AccountReference`, HTTPS `BaseUrl` and secret `ApiKey`. The owner approves a matching persisted bank connection and requests a pull. GET `{BaseUrl}/transactions?account={configured-reference}` uses a Bearer token and returns bounded normalized `externalId`, ISO `date`, decimal `amount` and `reference` records. Returned currency/account identifiers are checked when present; the configured gateway must normalize only its allowed USD account. Redirects are disabled and failures ingest no partial data. Credentials never enter Finance commands/state. This is an executable gateway adapter; no credentialed Plaid connection is claimed.

Fixed-asset capitalization reclassifies a linked, unused recorded purchase expense into a dedicated asset account, preserving equipment/source evidence. Advanced depreciation is outside this scope. Vendor compliance tracks W-9 status, insurance/license expiration, classification and attachment references without storing sensitive tax IDs. Tax-year 1099 source exports exclude explicitly classified payment-card/third-party-network methods. Filing remains an accountant-reviewed integration boundary.

## Bob, permissions and audit

Bob reads deterministic construction and operations results, cites source IDs, identifies incomplete data and limits stored findings. Tools explain projected loss/variance, under/overbilling, retainage, named backlog definitions, time/close blockers and expired insurance. Owner-only operations tools exclude labor rates and sensitive tax identifiers. Bob does not bypass EAC approval, time approval, retainage release, journal posting or period-close controls.

Services independently enforce financial read/write/control permissions and tenant boundaries. Commands include exact aggregate versions, identities and evidence. Source coordinators serialize conflicting payment/ledger writes; storage CAS and integrity hooks protect immutable accounting history. Payment intents/receipts additionally enforce source identity, allowed lifecycle transitions and settlement reconciliation.

## Construction migration

Construction migration extends the existing reviewed preview/import process. The normalizer accepts optional `construction.json` containing `constructionContracts`, `constructionHistory`, `constructionBudgets` and `constructionForecasts`. Contracts use canonical Job/customer IDs; history includes cutover billed, outstanding retainage, earned revenue, source IDs and per-SOV billing; budgets/forecasts retain explicit completeness evidence.

Construction controls require contract value, construction billed, retainage, earned revenue and budget source totals. Relationships, SOV/history totals and per-Job opening GL balances must reconcile before import. Historical earned revenue belongs in opening equity; later WIP recognizes the change since that baseline. Original source files are hashed into the immutable batch. Import does not invent approved TurnKey change orders.

AP retained-payable opening migration is not implemented by the existing opening adapter. Nonzero configured retained-payable opening balances are rejected until a reconciled retained-bill opening schedule is supported. This is a cutover boundary, not silent loss of retained liabilities.

## Verification and tracking

Final backend verification: 560 API tests passed; the 12 storage tests skipped by the ordinary suite passed separately. Construction/payment tests cover stored transfers, code allocation, tenant/account boundaries, durable checkout retry/cancellation, duplicate sessions, stale balances, closed/predating receipts and GL/subledger invariants. Migration normalizer: 9 passed. Client check: zero errors and warnings. Production build passed. Client unit tests: 62 passed. Full Finance browser suite: 38 passed (33.3 seconds), covering desktop/mobile and both-theme axe. Full Portal browser suite: 22 passed (27.8 seconds). UI browser tests use isolated contract fixtures; provider boundaries use mocks and locally HMAC-signed Stripe payloads, while storage suites exercise actual local emulator persistence. None of these checks claims a live credentialed merchant or bank connection.

Hubbsly parent `b1274d3d-2892-41f5-b8e5-7c92509e2923` tracks the work, including grouped cards TKO-0117–TKO-0130 and resumed AP retainage TKO-0131 and cost-code/payment dispatch TKO-0132. All 16 grouped implementation cards are done, and the parent is landed in the landing stage for local implementation. Standalone TKO-0133 (`b619b37c-81a6-455d-92e2-49bc27ba1aa0`) is ready and tracks the retained-AP opening migration adapter, which remains a separate cutover integration boundary. Existing V1 production gate TKO-0116 remains open. Landing records local implementation completion and does not claim deployment or production approval.

Remaining integration boundaries are payroll execution/provider delivery, 1099 filing, advanced depreciation, provider-specific banking adapters beyond the normalized gateway, merchant credential/connected-account onboarding and AP retained-payable opening schedules. Operational employee display names and deeper cross-module source-document links can be improved without changing financial authority. The next workstream should prioritize controlled cutover/onboarding and provider reconciliation before adding payroll execution or more revenue-recognition methods.
