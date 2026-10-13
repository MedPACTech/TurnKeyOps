using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

// Dimensional attribution of an existing expense, never a second journal or expense.
public static class FinanceBillExtraAllocations
{
    public const string GrossSource = "bill-extra-allocation";
    public const string CreditSource = "bill-extra-credit-allocation";

    public static void Allocate(FinanceState state, FinanceCommand command, string actor)
    {
        FinanceLedger.Required(actor); FinanceLedger.Required(command.Reason);
        if (!Guid.TryParse(command.Target, out var id)) throw new ArgumentException("Choose a posted vendor bill.");
        var bill = state.Bills.SingleOrDefault(b => b.Id == id && b.JournalId is not null) ?? throw new ArgumentException("Choose a posted vendor bill.");
        var source = Source(state, bill);
        RequireOpen(state, source);
        if (command.Date != bill.Date) throw new ArgumentException("Use the original bill accounting date for its cost allocation.");
        if (Gross(state, bill).Length != 0) throw new ArgumentException("Bill freight/tax is already allocated. Preserve its source evidence.");
        if (command.Lines is null || command.Lines.Length is < 1 or > 500 || command.Lines.Any(l => l is null)) throw new ArgumentException("Provide freight/tax allocation lines.");
        var jobs = bill.Lines.Where(l => l.JobId.HasValue).Select(l => l.JobId!.Value).ToHashSet();
        var account = source.Lines.Where(l => l.Debit>0).Last().Account;
        foreach (var line in command.Lines)
        {
            FinanceLedger.Positive(line.Debit);
            if (line.Credit != 0 || line.Account != account || line.JobId is not Guid job || !jobs.Contains(job)
                || !FinanceLedger.Categories.Contains(line.Category ?? "") || line.CostCode is null
                || !state.CostCodes.Any(c => c.Code == line.CostCode && c.Category == line.Category && c.Active)
                || line.CustomerId.HasValue || line.VendorId.HasValue || line.Trade is not null || line.Site is not null)
                throw new ArgumentException("Allocate the original freight/tax expense to its existing bill Jobs using positive debit amounts and active category-matching cost codes.");
        }
        if (command.Lines.Sum(l => l.Debit) != bill.Tax + bill.Shipping) throw new ArgumentException("Allocate exactly the bill's full freight and tax amount.");
        var gross = command.Lines.Select((line, index) => new FinanceJobCost(Guid.NewGuid(), line.JobId!.Value, line.Category!, line.CostCode,
            line.Debit, bill.Date, GrossSource, $"{bill.Id:D}:{index}", source.Id)).ToArray();
        var credits = CreditPlans(state, bill, gross, DateOnly.MaxValue).ToArray();
        // Attribution after an earlier credit changes that credit date's Job dimensions too.
        foreach (var journalId in credits.Select(c => c.JournalId).Distinct()) RequireOpen(state, state.Journals.Single(j => j.Id == journalId));
        PreflightCredits(state, credits);
        state.Costs.AddRange(gross);
        AppendCredits(state, credits);
        Invalidate(state, gross.Select(c => c.JobId).ToHashSet());
    }

    public static bool Outstanding(FinanceState state, Guid jobId, DateOnly asOf)
    {
        foreach (var bill in state.Bills.Where(b => b.JournalId is not null && b.Date <= asOf && b.Tax + b.Shipping > 0 && b.Lines.Any(l => l.JobId == jobId)))
        {
            var gross = Gross(state, bill);
            if (!Complete(bill, gross)) return true;
            try
            {
                Source(state, bill);
                if (CreditPlans(state, bill, gross, asOf).Any(plan => !state.Costs.Any(c => Matches(c, plan)))) return true;
            }
            catch (ArgumentException) { return true; }
        }
        return false;
    }

    // Called after a vendor-credit settlement is appended, in the same conditional Finance commit.
    public static void ApplyCredits(FinanceState state)
    {
        var plans = new List<CreditPlan>();
        foreach (var bill in state.Bills.Where(b => b.JournalId is not null && b.Tax + b.Shipping > 0))
        {
            var gross = Gross(state, bill);
            if (gross.Length == 0) continue; // Unknown attribution keeps Job profitability gated.
            if (!Complete(bill, gross)) throw new ArgumentException("Recorded bill extra attribution is incomplete; review its preserved source evidence.");
            plans.AddRange(CreditPlans(state, bill, gross, DateOnly.MaxValue));
        }
        PreflightCredits(state, plans);
        AppendCredits(state, plans);
    }

    private static FinanceJournal Source(FinanceState state, FinanceBill bill)
    {
        FinanceLedger.Money(bill.Tax); FinanceLedger.Money(bill.Shipping);
        var total = bill.Tax + bill.Shipping;
        if (bill.Tax < 0 || bill.Shipping < 0 || total <= 0) throw new ArgumentException("This bill has no positive freight/tax expense to allocate.");
        var source = state.Journals.SingleOrDefault(j => j.Id == bill.JournalId && j.SourceType == "vendor-bill" && j.SourceId == bill.Id.ToString() && j.Date == bill.Date)
            ?? throw new ArgumentException("The immutable source bill journal is unavailable.");
        var offsets = source.Lines.Where(l => l.Debit>0).ToArray();
        if (offsets.Length != bill.Lines.Count + 1 || offsets.Sum(l => l.Debit) != bill.Total) throw new ArgumentException("Bill expense coding does not reconcile to its immutable journal.");
        var extra = offsets[^1];
        if (extra.Account != state.Policy.Accounts["cost"] || extra.JobId.HasValue || extra.Debit != total || extra.Credit != 0
            || extra.Description != "Purchase tax and shipping (unallocated)" || extra.VendorId != bill.VendorId
            || !state.Accounts.Any(a => a.Code == extra.Account && a.Type is "EXPENSE" or "COST_OF_GOODS_SOLD"))
            throw new ArgumentException("The original freight/tax expense cannot be identified without inferring source coding.");
        return source;
    }

    private static void RequireOpen(FinanceState state, FinanceJournal journal)
    {
        if(state.Construction.WipSnapshots.Any(w=>w.Status=="POSTED"&&state.Periods.Any(p=>p.Id==w.PeriodId&&journal.Date<=p.End)))throw new ArgumentException("Posted WIP locks prior bill/credit cost attribution; record corrections in the next open period.");
        if (!state.Periods.Any(p => p.Id == journal.PeriodId && p.Status == "OPEN" && journal.Date >= p.Start && journal.Date <= p.End))
            throw new ArgumentException("Reopen the affected bill/credit accounting period before changing its Job cost attribution.");
    }

    private static FinanceJobCost[] Gross(FinanceState state, FinanceBill bill) => state.Costs
        .Where(c => c.SourceType == GrossSource && c.SourceId.StartsWith(bill.Id.ToString("D") + ":", StringComparison.Ordinal))
        .OrderBy(c => int.TryParse(c.SourceId[(c.SourceId.LastIndexOf(':') + 1)..], out var index) ? index : int.MaxValue).ToArray();

    private static bool Complete(FinanceBill bill, FinanceJobCost[] gross) => gross.Length > 0 && gross.Sum(c => c.Amount) == bill.Tax + bill.Shipping
        && gross.Select((c, index) => c.SourceId == $"{bill.Id:D}:{index}" && c.Date == bill.Date && c.JournalId == bill.JournalId && c.Amount > 0
            && c.CostCode is not null && FinanceLedger.Categories.Contains(c.Category) && bill.Lines.Any(l => l.JobId == c.JobId)).All(valid => valid);

    private sealed record CreditPlan(string Key, Guid JobId, string Category, string? CostCode, decimal Amount, DateOnly Date, Guid JournalId);
    private static IEnumerable<CreditPlan> CreditPlans(FinanceState state, FinanceBill bill, FinanceJobCost[] gross, DateOnly asOf)
    {
        var source = Source(state, bill);
        var offsets = source.Lines.Where(l => l.Debit>0).ToArray();
        foreach (var settlement in state.Settlements.Where(s => s.Kind == "vendor-credit" && s.Date <= asOf))
        {
            var journal = state.Journals.SingleOrDefault(j => j.Id == settlement.JournalId && j.SourceType == "vendor-credit");
            if (journal is null) continue; // Applying an opening credit does not reverse an expense.
            foreach (var (allocation, allocationIndex) in settlement.Allocations.Select((a, i) => (a, i)).Where(p => p.a.OpenItemId == bill.Id))
            {
                FinanceLedger.Positive(allocation.Amount);
                if (allocation.Amount > bill.Total || settlement.Date != journal.Date) throw new ArgumentException("Vendor credit source amounts/dates do not reconcile.");
                var sourceAmounts = Split(allocation.Amount, offsets.Select(l => l.Debit).ToArray());
                var extraShare = sourceAmounts[^1];
                if (extraShare == 0) continue;
                var shares = Split(extraShare, gross.Select(c => c.Amount).ToArray());
                for (var index = 0; index < shares.Length; index++)
                    if (shares[index] > 0) yield return new($"{settlement.Id:D}:{bill.Id:D}:{allocationIndex}:{index}", gross[index].JobId,
                        gross[index].Category, gross[index].CostCode, -shares[index], settlement.Date, journal.Id);
            }
        }
        foreach(var retainedCredit in state.Operations.ApRetainageEvents.Where(e=>e.BillId==bill.Id&&e.Kind=="CREDIT"&&e.Date<=asOf))
        {
            var journal=state.Journals.SingleOrDefault(j=>j.Id==retainedCredit.JournalId&&j.SourceType=="ap-retainage-credit")??throw new ArgumentException("Retained gross credit journal is unavailable.");
            var sourceAmounts=Split(retainedCredit.GrossAmount,offsets.Select(l=>l.Debit).ToArray());var extraShare=sourceAmounts[^1];if(extraShare==0)continue;
            var shares=Split(extraShare,gross.Select(c=>c.Amount).ToArray());
            for(var index=0;index<shares.Length;index++)if(shares[index]>0)yield return new($"{retainedCredit.Id:D}:{bill.Id:D}:retained:{index}",gross[index].JobId,gross[index].Category,gross[index].CostCode,-shares[index],retainedCredit.Date,journal.Id);
        }
    }

    // Same source-offset largest-remainder rule as FinanceLedger.Settle, with stable input-order ties.
    private static decimal[] Split(decimal amount, decimal[] weights)
    {
        var total = weights.Sum();
        if (total <= 0 || weights.Any(w => w < 0)) throw new ArgumentException("Credit attribution weights must reconcile to a positive source expense.");
        var exact = weights.Select(w => amount * w / total).ToArray();
        var result = exact.Select(a => decimal.Floor(a * 100) / 100).ToArray();
        var cents = (int)((amount - result.Sum()) * 100);
        foreach (var index in Enumerable.Range(0,weights.Length).OrderByDescending(i => exact[i] - result[i]).ThenBy(i => i).Take(cents)) result[index] += 0.01m;
        return result;
    }

    private static bool Matches(FinanceJobCost cost, CreditPlan plan) => cost.SourceType == CreditSource && cost.SourceId == plan.Key
        && cost.JobId == plan.JobId && cost.Category == plan.Category && cost.CostCode == plan.CostCode && cost.Amount == plan.Amount
        && cost.Date == plan.Date && cost.JournalId == plan.JournalId;

    private static void PreflightCredits(FinanceState state, IEnumerable<CreditPlan> plans)
    {
        foreach (var plan in plans)
        {
            var existing = state.Costs.SingleOrDefault(c => c.SourceType == CreditSource && c.SourceId == plan.Key);
            if (existing is not null)
            {
                if (!Matches(existing, plan)) throw new ArgumentException("A credit attribution source key already has conflicting immutable content.");
                continue;
            }
            RequireOpen(state, state.Journals.Single(j => j.Id == plan.JournalId));
        }
    }

    private static void AppendCredits(FinanceState state, IEnumerable<CreditPlan> plans)
    {
        var affected = new HashSet<Guid>();
        foreach (var plan in plans)
        {
            var existing = state.Costs.SingleOrDefault(c => c.SourceType == CreditSource && c.SourceId == plan.Key);
            if (existing is not null)
            {
                if (!Matches(existing, plan)) throw new ArgumentException("A credit attribution source key already has conflicting immutable content.");
                continue;
            }
            state.Costs.Add(new(Guid.NewGuid(), plan.JobId, plan.Category, plan.CostCode, plan.Amount, plan.Date, CreditSource, plan.Key, plan.JournalId));
            affected.Add(plan.JobId);
        }
        Invalidate(state, affected);
    }

    private static void Invalidate(FinanceState state, HashSet<Guid> jobs)
    {
        for (var index = 0; index < state.Budgets.Count; index++)
            if (jobs.Contains(state.Budgets[index].JobId)) state.Budgets[index] = state.Budgets[index] with
            { ActualsComplete = false, CompletenessNote = "Bill freight/tax attribution or its vendor credit changed. Review Job cost completeness against the preserved source journals." };
    }
}
