# Inventory & Purchasing V1 — Operational Supply

Local implementation, 2026-10-06. No production deployment, data seeding, vendor message or customer message was performed.

## Discovery and scope

This work extends the existing monorepo and the completed local Jobs/Estimates implementation. Discovery covered JobExecutionService/Rules/Configuration, accepted Estimate selected price lines and tenant pricing catalogs, Calendar events/reservations, ManagedPeople vendor contacts, per-user module restrictions, role permission registration, Bob action providers/policies, iBeam communications/blob storage, and existing mobile workspaces. No existing inventory/purchasing domain was present.

The universal model uses separate catalog items, locations, movement records, reservations, Job demand, vendor supply profiles/offers, purchase requests, orders, receipts, files and audit records. There are no trade-specific stock tables. A tenant can share these records across concrete, framing, land-clearing, doors-locks and future configured trades. Finance, AP, GL, payments and serialized inventory remain outside this module.

Hubbsly parent: `5283ceba-e8dd-4433-b0f8-0cc2bb5a7250`, **Inventory & Purchasing V1 — Operational Supply**. Children TKO-0061 through TKO-0067 group the requested work by implementation boundary. Related TKO-0021 (opening templates/sample hardware) and TKO-0028 (offline synchronization) retain their separate scope; no sample product costs or offline transaction queue were introduced.

## Persistence, consistency and audit

`SupplyStore` stores private immutable JSON versions through the existing iBeam blob interface in `operational-supply`. A tenant partition in Azure Table `SupplyVersions` points to the current payload. Conditional creation/update of that pointer commits catalog, demand, ledger, reservation, order, receipt and audit effects together. Failed compare-and-swap cannot double-reserve stock or partially post a receipt. Separate processes use the same physical ETag boundary; correctness does not depend on an in-process lock.

Each snapshot links its previous blob. Movements record quantities, units, locations, Job/demand/source identities, actor, time and reason. There is no editable on-hand quantity. A stable command ID and fingerprint reject changed-body replays; an identical replay returns the current workspace without repeating effects. The caller must refresh after another supply write. Uploaded evidence retains private blob identity and a SHA-256 hash.

This V1 uses one commit boundary per tenant, which intentionally serializes competing supply writes. It reads/replays the tenant payload rather than offering indexed, paginated warehouse reporting. Large catalogs/ledgers will need partitioned aggregates and projections before high-volume rollout. Preserve immutable versions and referenced files in storage retention rules. Unreferenced uploads after a failed save require later retention cleanup; ambiguous pointer failures never delete a possibly committed payload.

## Catalog, units, vendors and profiles

The catalog defines an item; it never owns stock quantity. Kinds include material, product, consumable, equipment part, service, subcontract, fee, rental and other. Stable IDs/SKUs/external identities support later scanning and supplier imports. Service/subcontract/fee/rental items are non-stock. Job-specific products cannot enter ordinary stock through adjustment or transfer.

Trade metadata is validated against configured profile fields, including concrete specifications and doors/hardware finish, handing, keying and compatibility. Profiles control default sourcing, readiness severity, preferred units and required delivery events. Tenant policy additionally supplies purchasing thresholds, preferred-vendor/rush/manual-price approval rules, per-user spending limits and Bob action modes. Owners edit the main rules and profiles in Purchasing; the complete contract remains available through the API.

Units use explicit, item-specific conversions. No density, pack size, volume or price conversion is guessed. A legacy unit label can be configured with a reviewed conversion to the catalog base unit. Purchase-order units must match mapped demand units; supplier pack costs must be explicitly converted before an order is drafted. Historical Estimate catalogs remain price-list/source data; accepted material requirements are explicitly linked to the universal catalog instead of rewriting sold documents or treating labor, allowance, markup and bundled scope as stock.

Vendors extend existing active tenant Contacts with a vendor profile; they do not create another identity directory. Multiple vendor offers per item record cost/unit/pack size/lead time. Source suggestions prefer configured suppliers, then recorded lead time, and explain that vendor availability still needs confirmation. They do not silently choose the cheapest vendor.

## Demand, stock and Job readiness

An existing Job material requirement is explicitly mapped into supply demand. The mapping captures its source, Job, trade, quantity, unit, required date and optional shared Calendar event. Catalog conversion is deterministic. Freeform and purchased-service demand can use direct sourcing without a warehouse location. New demand on closed, completed or cancelled Jobs requires reopening.

Stock operations include verified opening/count adjustments, reservations/releases, warehouse/truck transfers, issue to Job, consumption, returns and scrap. Reservations reduce availability without changing physical on-hand. Transfers conserve stock. Consumption is limited to issued/directly received quantities, and returns are limited to the unconsumed Job allocation. An open purchase cannot be duplicated by reserving the same unmet quantity. Rejected/damaged receipts are tracked separately and never count as available stock.

`SupplyJobReadiness` reads authoritative supply for mapped requirements. Unfulfilled quantity, partial/unacknowledged orders, late arrivals, missing required delivery events and changed requirement mappings become machine-readable supply state and explainable Job blockers. Warning profiles and reasoned owner overrides are supported. Moving work earlier uses current shared Calendar dates to identify delivery mismatch. Job requirement status is projected from supply for display; the old manual requirement writer cannot mark a mapped requirement available. Existing unmapped requirements retain compatibility behavior until explicitly adopted. An accepted Job change can add a new material requirement and map new demand.

Readiness is evaluated at Job read/transition time. Job, Calendar and supply remain separate aggregates; there is no distributed transaction locking a Job start together with a simultaneous supply change. These are operational checks, not a guarantee that stock cannot change after a readiness read.

## Purchasing and receiving

The flow is demand → purchase request → owner request approval → PO draft → policy-based PO approval → vendor send → acknowledgment → partial/full receipt → allocation/consumption → close. Buyers can also draft directly from demand. Orders cannot skip approval, over-order demand already covered by stock/orders, silently alter unit cost after approval, or cancel received goods. Policy changes invalidate an unsent approval; approval reasons are recalculated before approval.

Warehouse receipts increase on-hand and reserve accepted units for the originating Job in the same commit. Direct receipts satisfy the Job without creating general stock. Each receipt records accepted and rejected quantities, reference, actor/time, location and optional packing-slip/photo/PDF evidence. Rejected quantity remains outstanding for replacement. Over-receipt and wrong-unit submissions are rejected. Special-order/service returns requiring vendor credit/replacement resolution are not a financial returns/AP workflow.

Vendor acknowledgment is staff-recorded with a vendor reference and confirmed expected date. PO email uses existing iBeam transport and the configured tenant sender/vendor ordering address. A durable frozen send claim precedes the provider call. Concurrent/repeated requests make one provider attempt; a timeout or interrupted result remains unconfirmed and is never automatically resent. Provider acceptance is explicitly distinct from vendor delivery/acknowledgment. An owner can record externally verified sending with a reference after reviewing provider history. Automated delivery-webhook reconciliation and unattended retries are later transport work.

Order files accept vendor quotes, confirmation, PO PDF and specifications. Receipt files accept packing slips and field evidence. Files remain private and tenant/module-authorized. Browser-supplied blob paths cannot be used to retrieve arbitrary files.

## Shared scheduling

Supply delivery forms call `JobExecutionService.ScheduleAsync` and then link the created event to demand. They do not introduce a second calendar. Delivery events can omit internal crew assignments, use the existing Calendar storage/concurrency checks and are visible in the Job/Calendar workspace. A delivery cannot substitute for the work event required to start a Job or move the Job's work-date summary. If Calendar creation succeeds but supply linking conflicts, the Calendar event is retained for review; no delivery success is fabricated.

## Authorization and Bob

Inventory and Purchasing have independent read/write permission keys registered in both the API catalog and per-user module UI. Owner/admin role mappings explicitly include the new keys. Existing Staff/Member defaults do not automatically receive purchasing or inventory grants; configure their role permissions and explicit module access. Jobs permission does not grant either new module. Buying can expose operational demand without granting stock mutation; cost and supplier-commercial fields require Purchasing read. Inventory read cannot mutate; receiving uses Inventory write even when entered from the purchasing queue.

`SupplyAuthority` enforces authenticated tenant context, role permission and per-user module allowance at service boundaries. Related Job, Calendar, vendor, catalog and location references are tenant validated. Bob providers use those same services and the existing proposal/approval/audit executor. Supported supply tools include availability, shortages, source options, purchasing review, reservation, purchase request, PO draft and approved PO send. Read-only, disabled, approval and automatic modes are enforced without replacing business approval or stock validation. Receiving remains an authorized human action. Bob's workspace review is based on stored records, not invented availability or prices; no unattended monitoring worker was added.

## Shared UX and mobile

All three tenant shells expose `/admin/inventory` and `/admin/purchasing`. Inventory starts with needs attention and Job demand; stock lists and movement history are secondary. Purchasing has To buy, Waiting, Incoming, Late, Receiving and All orders views. Inline disclosure reveals sourcing, reservations, delivery scheduling, receiving, files and configuration only when needed. Existing Tailwind/semantic tokens, Lucide icons and teal/orange accents are retained.

The mobile PWA caches only a public offline recovery document. It does not cache customer/order data, queue inventory mutations or claim offline synchronization. Mutations require hydration and an online connection. Tabs are disabled until hydration to prevent lost early clicks. Forms have labels, visible focus, semantic status/error output and touch-sized controls.

## Reporting and migration foundation

The workspace reports stored shortages, late orders, receipt counts, accepted/rejected quantities grouped by unit and authorized order value. Ledger/audit, real request/order/approval/send/receipt times and Job/trade/vendor identities support downstream reporting. Quantities of different units are not added into a fabricated total.

Catalog source-system/external identities and command idempotency support reviewed single-record migration. Existing stock can be loaded through reasoned, auditable adjustments. There is no Acumatica connector, bulk CSV mapper or invented opening balance. Existing sold Estimates, Jobs and Calendar records are preserved.

## Verification and rollout

Final local verification:

- API: **323 passed**, five storage checks skipped in the default run.
- Isolated Azurite storage: **5 passed**, including the supply pointer/reservation race.
- Client unit tests: **54 passed**.
- Svelte check: **0 errors, 0 warnings**; production client build passed.
- Supply Playwright/axe: **12 passed** across desktop/mobile, light/dark, keyboard reservation, partial/damaged receipt, read-only controls, offline mutation prevention and recovery-only PWA.
- Existing Jobs, Leads and Estimates Playwright: **16 + 10 + 16 passed**.
- `git diff --check`: passed.

The first browser run caught a pre-hydration tab click race; tabs now wait for hydration, and the final suites pass. The .NET runner required permission to open its localhost test socket. No live transport, production records or deployment were used.

Verification commands:

```sh
dotnet build api/TurnKeyOps.Authorization.Tests --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet test api/TurnKeyOps.Authorization.Tests --no-build --no-restore
TKO_STORAGE_TESTS=1 dotnet test api/TurnKeyOps.Authorization.Tests --no-build --no-restore --filter 'Category=StorageIntegration'
```

From `client`:

```sh
npm run check
npm run build
node --test --experimental-strip-types tests/*.test.ts
npx playwright test --config playwright.supply.config.ts
npx playwright test --config playwright.jobs.config.ts
npx playwright test --config playwright.leads.config.ts
npx playwright test --config playwright.estimates.config.ts
```

API tests cover stock conservation, reservations, units, partial/damaged/direct receipt, approval/state guards, stale/replayed commands, cost redaction, foreign references, permissions, Bob authority, readiness and vendor-send uncertainty/concurrency. Isolated Azurite tests verify physical tenant pointers and competing reservations; that supply storage test uses in-memory blob payloads with the real Azure Table commit boundary. Browser/axe suites use explicit local fixture APIs, not production or a full live API deployment. Their results do not claim an independent comprehensive WCAG audit.

Before production use, provision `SupplyVersions` with the configured table prefix and private `operational-supply` blob storage; review tenant catalog/unit mappings, vendors, roles, approval rules, trade gates and sender configuration. Run a staging workflow and an explicitly authorized vendor-email smoke test. Retain previous immutable blobs and source Estimate attachments. Bulk imports, higher-volume storage projections, offline edits, serial/lot behavior, automated supplier integrations and provider reconciliation remain distinct follow-up work.
