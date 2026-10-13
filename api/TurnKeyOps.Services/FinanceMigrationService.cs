using System.Text.Json;
using MedInsights.Lib;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

/// <summary>A reviewable, single-cutover export-file adapter. All effects share the Finance ETag commit.</summary>
public sealed class FinanceMigrationService(IFinanceStore store, IFinanceAuthority authority, IUserContext user,
    ISupplyStore supplyStore, ICustomerRepository customers, IJobService jobs, IInvoiceService invoices, IFinanceSourceCoordinator? coordinator = null)
{
    public async Task<object> TemplateAsync(CancellationToken ct = default)
    {
        await authority.RequireAsync(false, ct);
        var state = await store.ReadAsync(user.TenantId, ct);
        var supply = await supplyStore.ReadAsync(user.TenantId, ct);
        var partition = RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
        return new
        {
            Request = new FinanceMigrationRequest
            {
                ExpectedVersion = state.Version, ExpectedSupplyVersion = supply.Version,
                Controls = new() { Cash = state.CashAccounts.Where(c => c.Active).ToDictionary(c => c.Id, _ => 0m) }
            },
            AccountChoices = state.Accounts.Where(a => a.Active).Select(a => new { a.Code, a.Name, a.Type, a.ExternalId }),
            CustomerChoices = (await customers.ListAsync(partition, ct)).Where(c => !c.IsDeleted && c.PartitionKey == partition).Select(c => new { c.Id, Name = c.CompanyName ?? (c.FirstName + " " + c.LastName).Trim() }),
            VendorChoices = supply.Vendors.Where(v => v.Active).Select(v => new { Id = v.ContactId, v.Name }),
            JobChoices = (await jobs.GetActiveAsync(ct)).Select(j => new { j.Id, j.Name }),
            InventoryChoices = supply.Catalog.Where(i => i.Active && i.Stocked).Select(i => new { i.Id, i.Name, i.Unit }),
            LocationChoices = supply.Locations.Where(l => l.Active).Select(l => new { l.Id, l.Name }),
            Steps = new[] { "Export/freeze source accounting", "Map accounts and canonical customer/vendor/job IDs", "Reconcile operational PO/stock conversion", "Enter source trial balance, signed open items and job schedules", "Review every dry-run control difference", "Owner approves exact fingerprint", "Reconcile the committed opening to source and release operations" }
        };
    }

    public async Task<FinanceMigrationPreview> PreviewAsync(FinanceMigrationRequest request, CancellationToken ct = default)
    {
        await authority.RequireAsync(false, ct);
        var state = await store.ReadAsync(user.TenantId, ct);
        var supply = await supplyStore.ReadAsync(user.TenantId, ct);
        return await Preview(state, supply, request, ct);
    }

    public async Task<object> ImportAsync(FinanceMigrationRequest request, CancellationToken ct = default)
    {
        await authority.RequireAsync(true, ct);
        if (!await authority.CanControlAsync(ct)) throw new ForbiddenAccessException("A Finance owner must approve the reconciled migration.");
        await using var fence = coordinator is null ? null : await coordinator.AcquireAsync(user.TenantId, ct);
        var state = await store.ReadAsync(user.TenantId, ct);
        var fingerprint = Fingerprint(request);
        var previous = state.MigrationBatches.SingleOrDefault(b => b.Id == request.Id);
        if (previous is not null)
        {
            if (previous.Fingerprint != fingerprint) throw new ArgumentException("Migration identity was already imported with different content.");
            return new { Saved = true, AlreadyImported = true, BatchId = previous.Id, previous.JournalId };
        }
        var supply = await supplyStore.ReadAsync(user.TenantId, ct);
        var preview = await Preview(state, supply, request, ct);
        if (!preview.CanImport) throw new ArgumentException(string.Join(" ", preview.Errors));
        if (request.ApprovedFingerprint != preview.Fingerprint) throw new ArgumentException("Review and approve the exact dry-run fingerprint before importing.");
        var prepared = Prepare(state, supply, request);
        var actor = user.UserId.ToString("D");
        var journal = FinanceLedger.Post(state, request.CutoverDate, "migration-opening", request.SourceSystem + ":" + request.ExportId,
            request.Reason, prepared.Lines, actor);
        FinanceConstructionMigration.Apply(state,request);
        foreach (var item in request.OpenItems)
        {
            var party = Entity(request, item.Kind == "AR" ? "customer" : "vendor", item.ExternalPartyId);
            var id = item.ExistingInvoiceId ?? Identity(request.SourceSystem, item.Kind, item.ExternalId);
            state.OpenItems.Add(new(id, item.Kind, party, item.Number, request.CutoverDate, item.DueDate, item.Balance, journal.Id,
                item.ExternalJobId is null ? null : Entity(request, "job", item.ExternalJobId), request.SourceSystem + ":" + item.ExternalId));
        }
        foreach (var cost in request.JobCosts)
            state.Costs.Add(new(Identity(request.SourceSystem, "job-cost", cost.ExternalId), Entity(request, "job", cost.ExternalJobId), cost.Category,
                cost.CostCode, cost.Amount, request.CutoverDate, "migration-job-cost", request.SourceSystem + ":" + cost.ExternalId, journal.Id));
        foreach (var commitment in request.Commitments)
            state.ImportedCommitments.Add(new(request.SourceSystem + ":" + commitment.ExternalId, request.Id, Entity(request, "job", commitment.ExternalJobId),
                Entity(request, "vendor", commitment.ExternalVendorId), commitment.ExternalId, commitment.Description, commitment.Category,
                commitment.RemainingAmount, request.CutoverDate, commitment.SupplyOrderId, commitment.SupplyOrderLineId,
                commitment.SupplyOrderId.HasValue ? supply.Orders.Single(o => o.Id == commitment.SupplyOrderId).Lines.Single(l => l.Id == commitment.SupplyOrderLineId).UnitCost : null));
        foreach (var inventory in request.Inventory)
            state.ImportedInventory.Add(new(request.SourceSystem + ":" + inventory.ExternalId, request.Id, inventory.ExternalId, inventory.ItemId,
                inventory.LocationId, inventory.Quantity, inventory.Value, request.CutoverDate));
        state.MigrationBatches.Add(new(request.Id, request.SourceSystem, request.ExportId, request.SourceFileSha256, request.CutoverDate, request.Strategy,
            fingerprint, journal.Id, actor, DateTime.UtcNow, request.Reason, JsonSerializer.Serialize(request, JobConfigurationService.Json), preview.Checks, request.Accounts, request.Entities));
        state.Audit.Add(new(request.Id, "migration-import", request.ExportId, actor, DateTime.UtcNow, request.Reason, fingerprint));
        // Supply is read-only here. An operator freezes source/supply during cutover; no distributed transaction is claimed.
        if ((await supplyStore.ReadAsync(user.TenantId, ct)).Version != request.ExpectedSupplyVersion)
            throw new InvalidOperationException("Purchasing/inventory changed during migration validation. Re-run the preview.");
        await store.SaveAsync(user.TenantId, state, request.ExpectedVersion, ct);
        return new { Saved = true, AlreadyImported = false, BatchId = request.Id, JournalId = journal.Id };
    }

    public async Task<object> ApplyOpeningCreditAsync(FinanceOpeningCreditApplication request, CancellationToken ct = default)
    {
        await authority.RequireAsync(true, ct);
        if (!await authority.CanControlAsync(ct)) throw new ForbiddenAccessException("A Finance owner must approve an imported credit application.");
        await using var fence = coordinator is null ? null : await coordinator.AcquireAsync(user.TenantId, ct);
        var state = await store.ReadAsync(user.TenantId, ct);
        var fingerprint = FinanceLedger.Hash(request);
        var previous = state.Audit.SingleOrDefault(a => a.CommandId == request.Id);
        if (previous is not null)
        {
            if (previous.Action != "opening-credit-application" || previous.Fingerprint != fingerprint)
                throw new ArgumentException("Credit application identity was already used with different content.");
            return new { Saved = true, AlreadyApplied = true };
        }
        if (request.Id == Guid.Empty || request.CreditItemId == request.TargetItemId || request.Date == default)
            throw new ArgumentException("Credit application requires distinct source/target items, command identity and date.");
        if (state.Version != request.ExpectedVersion) throw new InvalidOperationException("Finance changed. Refresh the imported credit and target balances.");
        FinanceLedger.Positive(request.Amount); FinanceLedger.Required(request.Reason);
        var credit = state.OpenItems.SingleOrDefault(i => i.Id == request.CreditItemId && i.Total < 0)
            ?? throw new ArgumentException("Choose an imported, unapplied source credit balance.");
        if (!state.Journals.Any(j => j.Id == credit.JournalId && j.SourceType is "migration-opening" or "opening-import"))
            throw new ArgumentException("This workflow applies only existing imported credit balances; it does not infer a new tax/revenue credit.");
        var target = state.OpenItems.SingleOrDefault(i => i.Id == request.TargetItemId && i.Kind == credit.Kind && i.PartyId == credit.PartyId && i.Total > 0)
            ?? throw new ArgumentException("Target must be an open invoice/bill for the same canonical party and subledger.");
        if (request.Date < credit.Date || request.Date < target.Date
            || request.Amount > -FinanceLedger.Balance(state, credit, DateOnly.MaxValue)
            || request.Amount > FinanceLedger.Balance(state, target, DateOnly.MaxValue))
            throw new ArgumentException("Application exceeds available source credit/target balance or predates an item.");
        var ap = credit.Kind == "AP"; var account = state.Policy.Accounts[ap ? "ap" : "ar"];
        var actor = user.UserId.ToString("D");
        FinanceLine[] lines = [
            new(account, request.Amount, 0, CustomerId: ap ? null : credit.PartyId, VendorId: ap ? credit.PartyId : null, Description: request.Reason),
            new(account, 0, request.Amount, CustomerId: ap ? null : credit.PartyId, VendorId: ap ? credit.PartyId : null, Description: request.Reason)];
        var journal = FinanceLedger.Post(state, request.Date, "opening-credit-application", request.Id.ToString(), request.Reason, lines, actor);
        // Signed paired allocations consume the negative source balance and the positive target equally.
        // The source credit already exists in opening GL: no revenue, tax, expense, Job cost or cash is invented.
        state.Settlements.Add(new(request.Id, ap ? "vendor-credit" : "credit", credit.PartyId, request.Date, request.Amount, "", "opening-credit",
            request.Reason, "opening-credit-application:" + request.Id, journal.Id,
            [new(target.Id, request.Amount), new(credit.Id, -request.Amount)], Origin: "opening-credit-application"));
        state.Audit.Add(new(request.Id, "opening-credit-application", target.Id.ToString(), actor, DateTime.UtcNow, request.Reason, fingerprint));
        await store.SaveAsync(user.TenantId, state, request.ExpectedVersion, ct);
        return new { Saved = true, AlreadyApplied = false };
    }

    public async Task<object> ReverseOpeningCreditAsync(FinanceOpeningCreditReversal request, CancellationToken ct = default)
    {
        await authority.RequireAsync(true, ct);
        if (!await authority.CanControlAsync(ct)) throw new ForbiddenAccessException("A Finance owner must approve reversing an imported credit application.");
        await using var fence = coordinator is null ? null : await coordinator.AcquireAsync(user.TenantId, ct);
        var state = await store.ReadAsync(user.TenantId, ct);
        var fingerprint = FinanceLedger.Hash(request);
        var prior = state.Audit.SingleOrDefault(a => a.CommandId == request.Id);
        if (prior is not null)
        {
            if (prior.Action != "opening-credit-reversal" || prior.Fingerprint != fingerprint)
                throw new ArgumentException("Credit reversal identity was already used with different content.");
            return new { Saved = true, AlreadyReversed = true };
        }
        if (request.Id == Guid.Empty || request.Date == default) throw new ArgumentException("Credit reversal identity and date are required.");
        if (state.Version != request.ExpectedVersion) throw new InvalidOperationException("Finance changed. Refresh before reversing the application.");
        FinanceLedger.Required(request.Reason);
        var original = state.Settlements.SingleOrDefault(s => s.Id == request.ApplicationId && s.Origin == "opening-credit-application")
            ?? throw new ArgumentException("Choose an original imported credit application in this tenant.");
        var source = state.Journals.Single(j => j.Id == original.JournalId);
        if (request.Date < original.Date || state.Journals.Any(j => j.ReversesId == source.Id))
            throw new ArgumentException("Application is already reversed or the correction predates it.");
        var actor = user.UserId.ToString("D");
        var reversal = FinanceLedger.Post(state, request.Date, "opening-credit-reversal", original.Id.ToString(), request.Reason,
            source.Lines.Select(l => l with { Debit = l.Credit, Credit = l.Debit, Description = request.Reason }).ToArray(), actor, source.Id);
        state.Settlements.Add(new(request.Id, original.Kind, original.PartyId, request.Date, original.Amount, "", "opening-credit-reversal",
            request.Reason, "opening-credit-reversal:" + request.Id, reversal.Id,
            original.Allocations.Select(a => new FinanceAllocation(a.OpenItemId, -a.Amount)).ToArray(), Origin: "opening-credit-reversal"));
        state.Audit.Add(new(request.Id, "opening-credit-reversal", original.Id.ToString(), actor, DateTime.UtcNow, request.Reason, fingerprint));
        await store.SaveAsync(user.TenantId, state, request.ExpectedVersion, ct);
        return new { Saved = true, AlreadyReversed = false };
    }

    private async Task<FinanceMigrationPreview> Preview(FinanceState state, SupplyState supply, FinanceMigrationRequest request, CancellationToken ct)
    {
        var errors = new List<string>();
        FinanceMigrationCheck[] checks = [];
        var lines = 0;
        try
        {
            var copy = JsonSerializer.Deserialize<FinanceState>(JsonSerializer.Serialize(state, JobConfigurationService.Json), JobConfigurationService.Json)!;
            var prepared = Prepare(copy, supply, request); checks = prepared.Checks; lines = prepared.Lines.Length;
            foreach (var mapping in request.Entities)
            {
                if (mapping.Kind == "customer")
                {
                    var partition = RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);
                    var customer = await customers.GetAsync(partition, RepositoryKeyHelper.ToRowKey(mapping.Id), ct);
                    if (customer is null || customer.IsDeleted || customer.Id != mapping.Id || customer.PartitionKey != partition)
                        errors.Add($"Customer mapping '{mapping.ExternalId}' is unavailable in this tenant.");
                }
                else if (mapping.Kind == "job")
                {
                    var job = await jobs.GetAsync(mapping.Id, ct);
                    if (job is null || job.Id != mapping.Id) errors.Add($"Job mapping '{mapping.ExternalId}' is unavailable in this tenant.");
                }
            }
            foreach(var contract in request.ConstructionContracts)
            {
                var job=await jobs.GetAsync(contract.JobId,ct);
                if(job is null||job.CustomerId!=contract.CustomerId)errors.Add("Construction contract customer does not match the canonical tenant job.");
            }
            foreach (var item in request.OpenItems.Where(i => i.ExistingInvoiceId.HasValue))
            {
                var invoice = await invoices.GetAsync(item.ExistingInvoiceId!.Value);
                if (item.Kind != "AR" || invoice is null || invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Void || invoice.Id != item.ExistingInvoiceId || invoice.CustomerId != Entity(request, "customer", item.ExternalPartyId)
                    || invoice.JobId != (item.ExternalJobId is null ? null : Entity(request, "job", item.ExternalJobId))
                    || invoice.IssueDate > request.CutoverDate.ToDateTime(TimeOnly.MaxValue) || item.Balance < 0 || item.Balance > invoice.Total)
                    errors.Add($"Existing invoice mapping '{item.ExternalId}' does not match its tenant/customer/job or source balance.");
            }
        }
        catch (MigrationControlException e) { checks = e.Checks; errors.Add(e.Message); }
        catch (ArgumentException e) { errors.Add(e.Message); }
        catch (InvalidOperationException e) { errors.Add(e.Message); }
        catch (OverflowException) { errors.Add("Migration amount exceeds the supported range."); }
        return new(Fingerprint(request), errors.Count == 0, errors.ToArray(),
            ["Freeze source accounting and purchasing/inventory until import and source-control sign-off are complete.",
             "Historical job costs are dimensional opening evidence already included in source GL/equity; they create no second expense.",
             "Inventory quantities must first match canonical Supply balances; valuation snapshots do not alter stock or post another material actual.",
             "Signed source credits remain AR/AP open items; an owner can explicitly apply them to same-party invoices/bills without inventing tax, cash or cost coding."],
            checks, lines, request.OpenItems?.Length ?? 0, request.JobCosts?.Length ?? 0, request.Commitments?.Length ?? 0, request.Inventory?.Length ?? 0);
    }

    private static (FinanceLine[] Lines, FinanceMigrationCheck[] Checks) Prepare(FinanceState state, SupplyState supply, FinanceMigrationRequest request)
    {
        if (request.Id == Guid.Empty || request.CutoverDate == default) throw new ArgumentException("Migration identity and cutover date are required.");
        if (request.ExpectedVersion != state.Version || request.ExpectedSupplyVersion != supply.Version)
            throw new InvalidOperationException("Finance or purchasing/inventory changed. Refresh the export mapping and dry run.");
        if (state.Audit.Any(a => a.CommandId == request.Id)) throw new ArgumentException("Migration identity was already used by another Finance command.");
        if (state.Accounts.Count == 0) throw new ArgumentException("Initialize the chart and accounting period before migration.");
        if (state.Journals.Count != 0 || state.OpenItems.Count != 0 || state.Costs.Count != 0 || state.MigrationBatches.Count != 0)
            throw new ArgumentException("Migration requires an unposted Finance workspace; historical/live accounting cannot overlap.");
        FinanceLedger.Required(request.SourceSystem); FinanceLedger.Required(request.ExportId); FinanceLedger.Required(request.Reason);
        if (string.IsNullOrEmpty(request.SourceFileSha256) || request.SourceFileSha256.Length != 64 || !request.SourceFileSha256.All(Uri.IsHexDigit)) throw new ArgumentException("Preserve the source export's SHA-256 digest.");
        if (request.Strategy != "OPEN_ITEMS_AND_BALANCES") throw new ArgumentException("Use OPEN_ITEMS_AND_BALANCES; full transaction history must not be mixed into this cutover.");
        if (state.Policy.Currency != "USD" || state.Policy.MaterialActualSource != "vendor-bill") throw new ArgumentException("This adapter supports USD and vendor-bill material costing.");
        if (request.Accounts is null || request.Entities is null || request.TrialBalance is null || request.OpenItems is null || request.JobCosts is null || request.Commitments is null || request.Inventory is null || request.NewAccounts is null || request.Controls?.Cash is null)
            throw new ArgumentException("All migration sections and controls must be supplied, using empty arrays where appropriate.");
        if (request.Accounts.Any(x => x is null) || request.Entities.Any(x => x is null) || request.TrialBalance.Any(x => x is null) || request.OpenItems.Any(x => x is null) || request.JobCosts.Any(x => x is null) || request.Commitments.Any(x => x is null) || request.Inventory.Any(x => x is null) || request.NewAccounts.Any(x => x is null))
            throw new ArgumentException("Migration sections cannot contain null rows.");
        if (request.OpenItems.Length + request.JobCosts.Length + request.Commitments.Length + request.Inventory.Length > 10000)
            throw new ArgumentException("Migration batch exceeds 10,000 detail rows; use a reviewed migration rehearsal before cutover.");
        Unique(request.Accounts.Select(m => m.ExternalAccount), "external account mapping");
        Unique(request.Entities.Select(m => m.Kind + ":" + m.ExternalId), "external entity mapping");
        Unique(request.TrialBalance.Select(b => b.ExternalAccount), "trial balance account");
        Unique(request.OpenItems.Select(i => i.Kind + ":" + i.ExternalId), "open item");
        Unique(request.OpenItems.Where(i => i.ExistingInvoiceId.HasValue).Select(i => i.ExistingInvoiceId!.Value.ToString()), "canonical invoice");
        Unique(request.JobCosts.Select(i => i.ExternalId), "job cost source");
        Unique(request.Commitments.Select(i => i.ExternalId), "commitment source");
        Unique(request.Commitments.Where(i => i.SupplyOrderLineId.HasValue).Select(i => i.SupplyOrderLineId!.Value.ToString()), "canonical PO line");
        Unique(request.Inventory.Select(i => i.ExternalId), "inventory source");
        Unique(request.Inventory.Select(i => i.ItemId + ":" + i.LocationId), "inventory item/location");
        foreach (var account in request.NewAccounts)
        {
            FinanceLedger.Required(account.Code); FinanceLedger.Required(account.Name);
            if (state.Accounts.Any(a => a.Code == account.Code) || !FinanceLedger.AccountTypes.Contains(account.Type) || account.NormalBalance is not ("DEBIT" or "CREDIT") || account.SystemControlled || !account.Active)
                throw new ArgumentException("New mapped accounts must be unique, active, non-system accounts with explicit classifications.");
            if (account.ParentCode is not null && !state.Accounts.Any(a => a.Code == account.ParentCode && a.ParentCode is null)) throw new ArgumentException("New account parent must be an existing top-level account.");
            state.Accounts.Add(account);
        }
        foreach (var map in request.Accounts)
            if (!state.Accounts.Any(a => a.Code == map.Account && a.Active)) throw new ArgumentException($"Missing active account for mapping '{map.ExternalAccount}'.");
        foreach (var map in request.Entities)
        {
            FinanceLedger.Required(map.ExternalId);
            if (map.Id == Guid.Empty || map.Kind is not ("customer" or "vendor" or "job")) throw new ArgumentException("Entity mappings require canonical customer/vendor/job IDs.");
            if (map.Kind == "vendor" && !supply.Vendors.Any(v => v.ContactId == map.Id && v.Active)) throw new ArgumentException($"Vendor mapping '{map.ExternalId}' is unavailable in this tenant.");
        }
        var lines = request.TrialBalance.Where(b => b.Debit != 0 || b.Credit != 0).Select(b =>
        {
            FinanceLedger.Money(b.Debit); FinanceLedger.Money(b.Credit);
            if (b.Debit < 0 || b.Credit < 0 || (b.Debit == 0) == (b.Credit == 0)) throw new ArgumentException("Source trial-balance rows require exactly one positive debit or credit.");
            var map = request.Accounts.SingleOrDefault(m => m.ExternalAccount == b.ExternalAccount) ?? throw new ArgumentException($"Unmapped source account '{b.ExternalAccount}'.");
            return new FinanceLine(map.Account, b.Debit, b.Credit, Description: "Opening source account " + b.ExternalAccount);
        }).ToArray();
        if(state.Operations.ApRetainageAccount.Length>0&&lines.Where(l=>l.Account==state.Operations.ApRetainageAccount).Sum(l=>l.Debit-l.Credit)!=0)throw new ArgumentException("AP retained-payable opening balances require a reconciled retained-bill opening schedule; this adapter does not yet support that schedule.");
        if (lines.Length is < 2 or > 500 || lines.Sum(l => l.Debit) != lines.Sum(l => l.Credit)) throw new ArgumentException("Opening trial balance must balance with 2–500 non-zero rows.");
        foreach (var item in request.OpenItems)
        {
            FinanceLedger.Required(item.ExternalId); FinanceLedger.Required(item.Number); FinanceLedger.Money(item.Balance);
            if (item.Kind is not ("AR" or "AP") || item.Balance == 0 && (item.Kind != "AR" || !item.ExistingInvoiceId.HasValue) || item.Date == default || item.Date > request.CutoverDate || item.DueDate < item.Date || item.ExistingInvoiceId == Guid.Empty)
                throw new ArgumentException("Open items require AR/AP, signed source balances, external IDs and valid dates. Zero balance is allowed only for an explicitly mapped canonical AR invoice.");
            Entity(request, item.Kind == "AR" ? "customer" : "vendor", item.ExternalPartyId);
            if (item.ExternalJobId is not null) Entity(request, "job", item.ExternalJobId);
        }
        foreach (var cost in request.JobCosts)
        {
            Entity(request, "job", cost.ExternalJobId); FinanceLedger.Money(cost.Amount);
            if (!FinanceLedger.Categories.Contains(cost.Category) || cost.Amount == 0 || cost.CostCode is not null && !state.CostCodes.Any(c => c.Code == cost.CostCode && c.Category == cost.Category && c.Active))
                throw new ArgumentException("Historical job cost requires non-zero amount and an active matching category/cost code.");
        }
        foreach (var commitment in request.Commitments)
        {
            var job = Entity(request, "job", commitment.ExternalJobId); var vendor = Entity(request, "vendor", commitment.ExternalVendorId);
            FinanceLedger.Required(commitment.Description); FinanceLedger.Positive(commitment.RemainingAmount);
            if (!FinanceLedger.Categories.Contains(commitment.Category) || commitment.SupplyOrderId.HasValue != commitment.SupplyOrderLineId.HasValue) throw new ArgumentException("Commitment requires a valid category and paired canonical order/line IDs.");
            if (commitment.SupplyOrderId.HasValue)
            {
                var order = supply.Orders.SingleOrDefault(o => o.Id == commitment.SupplyOrderId && o.VendorId == vendor && IsCommitted(o.Status)) ?? throw new ArgumentException("Mapped commitment PO is unavailable or not committed in this tenant.");
                var line = order.Lines.SingleOrDefault(l => l.Id == commitment.SupplyOrderLineId) ?? throw new ArgumentException("Mapped commitment PO line is unavailable.");
                if (!supply.Demands.Any(d => d.Id == line.DemandId && d.JobId == job) || commitment.RemainingAmount > line.Quantity * line.UnitCost)
                    throw new ArgumentException("Mapped commitment Job or remaining amount conflicts with the canonical PO line.");
            }
        }
        foreach (var inventory in request.Inventory)
        {
            FinanceLedger.Money(inventory.Value);
            if (inventory.Quantity < 0 || inventory.Quantity > 1_000_000_000_000m || inventory.Quantity != Math.Round(inventory.Quantity, 6) || inventory.Value < 0 || inventory.Quantity == 0 && inventory.Value != 0
                || !supply.Catalog.Any(i => i.Id == inventory.ItemId && i.Active && i.Stocked) || !supply.Locations.Any(l => l.Id == inventory.LocationId && l.Active))
                throw new ArgumentException("Inventory requires canonical stocked items/locations, non-negative quantities and source valuation.");
            var onHand = supply.Movements.Where(m => m.ItemId == inventory.ItemId).Sum(m => (m.ToLocationId == inventory.LocationId ? m.Quantity : 0) - (m.FromLocationId == inventory.LocationId ? m.Quantity : 0));
            if (onHand != inventory.Quantity) throw new ArgumentException($"Inventory '{inventory.ExternalId}' differs from Supply on-hand. Convert/reconcile operational stock before Finance cutover.");
        }
        var checks = new List<FinanceMigrationCheck>();
        void Check(string name, decimal imported, decimal source) { FinanceLedger.Money(source); checks.Add(new(name, imported, source, imported - source)); }
        checks.AddRange(FinanceConstructionMigration.Validate(state,request,lines));
        Check("AR source", request.OpenItems.Where(i => i.Kind == "AR").Sum(i => i.Balance), request.Controls.Ar);
        Check("AP source", request.OpenItems.Where(i => i.Kind == "AP").Sum(i => i.Balance), request.Controls.Ap);
        Check("AR GL", lines.Where(l => l.Account == state.Policy.Accounts["ar"]).Sum(l => l.Debit - l.Credit), request.Controls.Ar);
        Check("AP GL", lines.Where(l => l.Account == state.Policy.Accounts["ap"]).Sum(l => l.Credit - l.Debit), request.Controls.Ap);
        Check("Job cost-to-date", request.JobCosts.Sum(c => c.Amount), request.Controls.JobCosts);
        Check("Remaining commitments", request.Commitments.Sum(c => c.RemainingAmount), request.Controls.Commitments);
        Check("Inventory source", request.Inventory.Sum(i => i.Value), request.Controls.Inventory);
        Check("Inventory GL", lines.Where(l => l.Account == state.Policy.Accounts["inventory"]).Sum(l => l.Debit - l.Credit), request.Controls.Inventory);
        foreach (var cash in state.CashAccounts.Where(c => c.Active))
        {
            if (!request.Controls.Cash.TryGetValue(cash.Id, out var control)) throw new ArgumentException($"Source cash control '{cash.Id}' is required, including zero balances.");
            Check("Cash " + cash.Id, lines.Where(l => l.Account == cash.LedgerAccount).Sum(l => l.Debit - l.Credit), control);
        }
        if (request.Controls.Cash.Keys.Any(id => !state.CashAccounts.Any(c => c.Id == id && c.Active))) throw new ArgumentException("Source cash control refers to an unknown/inactive cash account.");
        if (checks.Any(c => c.Difference != 0)) throw new MigrationControlException(checks.ToArray());
        lines=FinanceConstructionMigration.DimensionOpening(state,request,lines);
        // Post validates active accounts and the actual cutover period on a throw-away state during dry run.
        var opening = FinanceLedger.Post(state, request.CutoverDate, "migration-opening", request.SourceSystem + ":" + request.ExportId, request.Reason, lines, "migration-preview");
        state.Journals.Remove(opening);
        if (checks.Any(c => c.Difference != 0)) throw new MigrationControlException(checks.ToArray());
        return (lines, checks.ToArray());
    }

    public static string Fingerprint(FinanceMigrationRequest request) => FinanceLedger.Hash(new { request.Id, request.ExpectedVersion, request.ExpectedSupplyVersion,
        request.SourceSystem, request.ExportId, request.SourceFileSha256, request.CutoverDate, request.Strategy, request.Reason, request.NewAccounts,
        request.Accounts, request.Entities, request.TrialBalance, request.OpenItems, request.JobCosts, request.Commitments, request.Inventory, request.Controls,request.ConstructionContracts,request.ConstructionHistory,request.ConstructionBudgets,request.ConstructionForecasts });
    private static Guid Entity(FinanceMigrationRequest request, string kind, string external) => request.Entities.SingleOrDefault(m => m.Kind == kind && m.ExternalId == external)?.Id
        ?? throw new ArgumentException($"Missing explicit {kind} mapping for '{external}'.");
    private static Guid Identity(string source, string kind, string external) => new(Convert.FromHexString(FinanceLedger.Hash(new { source, kind, external }))[..16]);
    private static void Unique(IEnumerable<string> keys, string label)
    {
        var list = keys.ToArray(); foreach (var key in list) FinanceLedger.Required(key);
        if (list.Distinct(StringComparer.Ordinal).Count() != list.Length) throw new ArgumentException($"Duplicate {label}; merge/export and review source IDs before importing.");
    }
    public static bool IsCommitted(string status) => status is "APPROVED" or "SENT" or "ACKNOWLEDGED" or "PARTIALLY_RECEIVED" or "RECEIVED";
    private sealed class MigrationControlException(FinanceMigrationCheck[] checks) : ArgumentException("Migration controls do not reconcile: " + string.Join(", ", checks.Where(c => c.Difference != 0).Select(c => c.Name + " difference " + c.Difference)))
    { public FinanceMigrationCheck[] Checks { get; } = checks; }
}
