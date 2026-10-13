# Finance insights, source review and controlled contract corrections

This describes the Finance insights extension. It does not certify a production cutover or a future cash balance.

## Cash scenarios

The guarded Finance workspace returns a reproducible 30-day forecast for its `asOf` date. Opening cash is the sum of posted debit-minus-credit balances for the distinct registered cash ledger accounts. Transfers between those accounts net to zero. Two end-of-day paths are returned:

1. **Recorded obligations scenario:** opening cash plus already-posted future cash movement, less remaining AP assumed paid on due dates. It assumes no new AR collections.
2. **Due-date scenario:** the same path plus remaining AR assumed collected on due dates.

Overdue AR/AP is scheduled on day one. A due date is a timing assumption, not evidence that a customer will pay or that a vendor payment is authorized. Future-dated posted allocations are reserved against each open item so the same future receipt/payment is not counted in both posted movement and assumed movement. Future-created invoices are excluded from the as-of open-item set. Signed unapplied opening credits offset only same-party, same-ledger due documents in the scenario; this assumption does not apply the credit in accounting or infer a cash refund. Each credit retains a zero-cash evidence row and an explicit allocation-review assumption. Every projected movement has its open-item or journal identity, document number, date, amount and assumption.

Missing payroll, overhead, tax payments, future sales, unbilled costs and unscheduled purchase commitments are explicitly unknown and excluded. Draft/unapproved bill totals are disclosed as excluded. Pending invoice dispatch can also affect Finance records. These scenarios must not be presented as guaranteed available spending or as a probabilistic best/worst case. They make no payments and post no journals.

## Bob reviews

Existing `finance.ar`, `finance.ap`, `finance.job-health`, `finance.cash` and `finance.close-readiness` tools return deterministic explanations with authoritative source IDs and a proposed next action. The provider verifies the live tenant and actor, and the service requires independent Finance read permission. The frontend renders returned findings; it does not substitute a canned success message for an analysis.

The stored Bob response includes at most five prioritized findings and five source references per finding, explicitly disclosing omitted records. Complete records remain in the guarded Finance views. Cash explanations include assumptions and exclusions; the complete daily/source forecast remains in the workspace to respect Bob's audited response storage budget. Bob performs no external communication, payment, approval, posting or close. Journal drafting retains its existing explicit approval flow and still creates only an unposted draft.

Job profit remains unknown if cost completeness, any budget category or tax basis is unknown. Historical migrated cost-to-date also blocks a current-period margin claim because matching historical revenue is not established by an opening AR/control import.

## Source drill-through and commitments

Job health exposes posted actual cost source type, source ID, accounting date, amount and its immutable journal. Journal drill-through switches the Finance view, opens the exact journal, and moves keyboard focus to it. Vendor costs retain the existing AP review. Signed Job change history displays its accepted estimate/revision/hash, original net value and compensating corrections.

Live PO source rows show canonical order, line and demand identities, ordered/billed quantities, unit price and remaining value. Only journals-backed approved bills (plus legacy POSTED snapshots) relieve a commitment. Draft bills do not become actuals. Imported remaining-value baselines replace the corresponding original PO baseline; mapped PO relief uses billed quantity at the committed PO unit price so actual bill price variance does not consume unrelated remaining units; their remaining quantity is unknown, so the UI does not fabricate a quantity-times-price explanation. External-only imported commitments use explicit imported source IDs on bill lines. Mapped operational POs use their canonical order/line instead, preventing duplicate relief.

## Cancelled signed changes

An authorized Finance owner can append an exact compensating contract correction after the canonical operational change is explicitly CANCELLED or DECLINED. The command requires a source key, reason, open-period accounting date and the exact negative of the originally signed net value. The source Job must be loaded through the tenant-scoped Job service. Replays are idempotent; changed content and duplicate compensations are rejected.

The original signed change is immutable. Correction history is append-only, actor/time stamped and included in the Finance command audit. The budget's approved-change total becomes original signed values plus compensations, and cost completeness is invalidated for review. This is a contract-value correction, not a GL reversal: invoice revenue/tax requires the existing credit workflow, and incurred costs remain incurred. Replacement scope needs a separately signed new change.

## Migration review UI

Accounting includes owner-controlled source-template loading, tenant mapping choices, normalized JSON paste/upload, preview errors/warnings, source-versus-imported control checks and row counts. Preview has no posting effects. Import submits the exact reviewed payload and preview fingerprint and requires an explicit reviewed-controls confirmation. Owners can also explicitly apply signed imported opening credits to same-party, same-ledger positive documents through a reviewed allocation workflow, or unapply a mistaken allocation with an audited compensating entry that restores both balances without deleting history. The API revalidates versions, owner access and controls. A changed payload cannot reuse a prior preview approval. See [finance-migration.md](finance-migration.md) for normalization and cutover requirements.

## Portal browser hardening

Portal forms use progressive enhancement, retaining native no-JavaScript submission while keeping sequential hydrated submissions on one page. Imported customer invoices show their original canonical invoice total and issue/due dates separately from the cutover remaining balance. Receipt history is labelled with its cutover boundary, and future-dated receipts are withheld from completed history. Browser tests wait for the layout hydration marker before interacting or scanning with axe. Earlier failure traces showed native message submission followed by page reloads before the next form POST. Development hot reload can independently erase in-progress form input; browser suites must run without concurrent client edits, Svelte synchronization or builds.

Local verification evidence is recorded in the parent Finance handoff after all agent changes are integrated. Fixture browser results do not prove production storage, payment-provider behavior or ERP cutover correctness.
