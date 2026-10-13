# Finance opening migration

This is a reviewed **open items plus opening balances** cutover, with normalized file adapters for Acumatica, QuickBooks, Sage and spreadsheet exports. It is not a live vendor connector. No credentials, balances, tax coding, party identities or missing Job costs are inferred. Full historical transaction migration is intentionally rejected by this adapter rather than mixed with summarized openings.

## Guided review

1. Freeze source accounting and take independently retained exports/backups. Agree an inclusive cutover date and obtain source trial balance, subsidiary schedules, cash controls, Job cost-to-date and remaining commitment schedules. Resume new invoice/payment activity on the day **after** cutover; late corrections require an explicit current-period accounting workflow.
2. Initialize Finance, configure accounts/posting policy, create an open calendar period containing cutover and configure active bank accounts. This adapter supports USD and vendor-bill material actuals. Opening migration is blocked after any GL posting, open item or Job actual exists.
3. In Finance → Accounting, load the migration template. It contains exact Finance/Supply versions and canonical tenant account/customer/vendor/active Job/item/location choices. Match external IDs explicitly. Create canonical operational customers/vendors/Jobs first; Finance does not create incomplete operational records from an accounting name. Optional `newAccounts` can add explicitly classified, active, non-system accounts atomically with the opening.
4. Convert/reconcile operational POs and stock using existing Purchasing/Inventory workflows. A migration commitment may link an existing canonical PO and line; this snapshot **replaces** that line's remaining amount for Finance rather than counting the PO twice. The canonical source unit cost is frozen with this baseline; subsequent bill quantities relieve it at that source cost, so actual bill price variances cannot consume unrelated unbilled units. Mapped bills must retain source vendor/Job/category and cannot exceed the verified source remaining quantity without a reviewed source amendment. An external-only commitment remains separately traceable and requires an explicit imported commitment ID when an approved bill relieves it. Stock valuation rows must match canonical item/location on-hand quantity exactly. Finance valuation snapshots never change stock quantities or create another material expense.
5. Upload or paste the normalized request, then run the dry run. Review every source-versus-import control and all warnings. Correct every non-zero difference and re-run. Preview never writes financial records. Editing any mapped data, control, date, reason or version invalidates the approval fingerprint.
6. An authorized Finance owner checks that the controls were independently reviewed and approves the exact dry-run fingerprint. Import revalidates tenant references, period, controls and source versions; it uses the shared invoice-source fence and the Finance conditional ETag commit. One transaction records the opening journal, open items, historical cost dimensions, commitment/inventory snapshots, immutable source payload/provenance and audit.
7. Compare the committed trial balance, AR/AP aging and bank balances to the independently retained exports. Review Job budget, revenue basis and cost completeness. Retain the export manifest and sign-off outside the application's storage as well. Release operations only after this reconciliation.

The invoice-source fence serializes in-app invoice writes with opening import. Purchasing/Inventory is a separate store: its version is rechecked immediately before the Finance save, but a distributed Supply/Finance transaction is not claimed. Keep operational stock/PO writes frozen through cutover. Production reconciliation and an independent accounting review remain required.

## File adapter

The script uses Python's standard library and never sends network requests or posts accounting data:

```sh
python3 scripts/finance-migration/normalize_exports.py /path/to/source-exports \
  --template /path/to/authenticated-template.json \
  --columns /path/to/reviewed-column-map.json \
  --source-system Acumatica \
  --export-id company-2026-09-cutover \
  --cutover 2026-09-30 \
  --reason 'Reviewed source trial balance and subsidiary schedules' \
  --out /path/to/finance-preview.json
```

Save the authenticated `GET /api/finance/migration/template` response as the template. API `{data: ...}` envelopes are accepted. Use freshly loaded versions; do not substitute wildcard versions. The script writes the normalized request and an adjacent `.manifest.json` containing original file SHA-256 hashes, filenames and row counts. A combined manifest digest becomes `sourceFileSha256`, including any explicit column mapping. Retain all original files and the manifest; the API cannot independently certify a source ERP's self-reported control totals.

CSV files are UTF-8 with headers. The following canonical headers are supported; blank optional fields are omitted. `accounts.csv` and `gl.csv` are required. Optional sections may be absent or header-only. `controls.json` is required, including zero values for every configured active cash account.

| File | Required headers | Optional headers |
| --- | --- | --- |
| accounts.csv | externalAccount, account | — |
| entities.csv | kind, externalId, id | — |
| gl.csv | externalAccount, debit, credit | — |
| open-items.csv | externalId, kind, externalPartyId, number, date, dueDate, balance | externalJobId, existingInvoiceId |
| job-costs.csv | externalId, externalJobId, category, amount | costCode |
| commitments.csv | externalId, externalJobId, externalVendorId, description, category, remainingAmount | supplyOrderId, supplyOrderLineId |
| inventory.csv | externalId, itemId, locationId, quantity, value | — |

`entities.kind` is `customer`, `vendor` or `job`; `id` is the existing canonical tenant GUID. Open-item kind is `AR` or `AP`. Amounts use plain decimal numbers without currency symbols or thousands separators, at most two decimal places. Quantities support six decimal places. Dates use ISO `YYYY-MM-DD`. Cost categories use the shared Finance categories: MATERIAL, LABOR, EQUIPMENT, SUBCONTRACT, OTHER_DIRECT_COST, BURDEN and OVERHEAD_ALLOCATED. A supplied cost code must exist, be active and match its category.

ERP report headers vary by export/report customization. Explicitly map source headers rather than depending on an assumed Acumatica schema, for example:

```json
{
  "trialBalance": {
    "externalAccount": "AccountCD",
    "debit": "DebitBalance",
    "credit": "CreditBalance"
  },
  "ignoreColumns": {"trialBalance": ["AccountDescription"]}
}
```

Unmapped extra columns fail normalization unless individually acknowledged under `ignoreColumns`; they are never silently dropped. No account/customer/vendor/Job matching by name is performed. Decimal output preserves cents directly without binary float conversion.

Example controls:

```json
{
  "ar": 100,
  "ap": 40,
  "jobCosts": 80,
  "commitments": 30,
  "inventory": 20,
  "cash": {"operating": 50}
}
```

Source GL must balance and agree with net signed AR/AP, inventory valuation and every active cash account control. Job costs and commitments reconcile to their independent source schedules. Missing cash controls, duplicate source IDs, duplicate canonical invoice/PO-line mappings, unmapped accounts/parties and tenant mismatches block import. A dry-run control mismatch returns the imported amount, source amount and difference for review.

## Source overlap and corrections

The cutover date is inclusive. Historical invoice originals and payment events on/before that date do not repost into GL. Map each existing operational invoice explicitly with `existingInvoiceId` if it has an imported open balance; the importer verifies customer, Job, date and source amount against the canonical invoice. Imported `FinanceOpenItem.Id` then equals that invoice ID, allowing later successful payment/refund events to settle the imported balance without posting the invoice revenue again. Explicitly mapped zero-balance AR rows are allowed to preserve source-paid evidence where historical cash lives only in the external ERP, including support for later authoritative provider refunds. Zero rows without a canonical AR invoice mapping are rejected. A post-cutover event for an omitted historical invoice is rejected for review rather than silently creating revenue or dropping activity.

Signed negative open items preserve unapplied customer/vendor source credits in net AR/AP controls. These are opening credit balances, not reconstructed historical revenue/tax/cost credits. An owner can apply an existing imported negative credit balance to a positive invoice/bill for the same canonical party and subledger with `POST /api/finance/migration/apply-credit`. The request contains `id`, `expectedVersion`, `date`, `creditItemId`, `targetItemId`, `amount` and `reason`. It validates both remaining balances and the open period, records balanced debit/credit lines in the same AR/AP control account (zero net GL effect), and appends paired signed allocations. No cash, expense, revenue, tax or historical Job cost is invented: the source credit was already recognized in opening GL. Applications are audited, immutable and idempotent; cross-party use and overapplication are rejected. An owner can unapply an accidental application with `POST /api/finance/migration/reverse-credit`, supplying `id`, `expectedVersion`, `date`, `applicationId` and `reason`. This appends a linked inverse journal and paired inverse allocations, restores both open balances, preserves the original evidence and allows only one reversal per application in an open period. Imported historical cash is not fabricated as payment history; only explicit observed receipts appear as receipts.

Job cost-to-date is appended as `migration-job-cost` dimensional evidence linked to the opening journal. Source opening GL/equity already includes that historical financial effect, so no second expense is posted. Historical Job earned revenue may not be available in a summarized opening; keep profitability unknown until its source basis can be established, even when cost totals reconcile. Inventory valuations likewise provide opening asset evidence without a vendor-bill duplicate. This is not perpetual inventory valuation: under vendor-bill costing, subsequent stock consumption does not automatically relieve the imported inventory GL asset or allocate its historical value to a new Job. Agree and review periodic inventory adjustments and Job allocation policy before cutover; keep affected profitability incomplete where that cost basis is missing. Automated ongoing inventory valuation remains outside this adapter.

Imported AP vendor/document numbers remain reserved: a new vendor bill cannot repost an existing source invoice, including normalized whitespace/case matches. Resolve genuine source corrections through reviewed accounting evidence. Export parsing and source-manifest checksums use the same byte snapshot, so retained provenance identifies the actual imported file contents.

Replaying the same imported migration ID and fingerprint is a no-op even after versions change. Reusing that identity with changed data fails. New imports into an already posted workspace fail. Migration batches, costs, open items, commitments, inventory valuations and original posted journals cannot be edited/deleted; future corrections must append explicit reviewed accounting evidence. This adapter does not implement destructive rollback, full history replacement, a live ERP API connector or an automatic cross-store operational migration.

## Local verification

Dedicated API tests: **30 passed, 0 failed, 0 skipped** locally. They cover dry-run non-mutation, exact approval, atomic/idempotent imports, AR/AP and secondary controls, signed source credits and explicit owner applications/reversals, zero-balance canonical AR evidence, canonical tenant references, period locks, historical overlap, source metadata, stock reconciliation, commitment replacement/relief, imported vendor-invoice duplicate prevention and immutable history. Python adapter tests: **8 passed**. They cover cent precision, source manifests, explicit ERP header maps, unknown-column rejection, malformed values, JSON escaping and provenance hashes over the exact bytes parsed. Run them with:

```sh
python3 -m unittest discover -s scripts/finance-migration -p 'test_*.py'
dotnet test api/TurnKeyOps.Authorization.Tests --filter FullyQualifiedName~FinanceMigrationTests
```

Local fixture tests are not production ERP reconciliation. Validate representative customer exports, cloud storage, permissions, backup/restore, throughput and accounting sign-off before declaring a customer's migration successful.
