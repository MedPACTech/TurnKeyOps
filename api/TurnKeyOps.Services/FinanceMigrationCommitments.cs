using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public static class FinanceMigrationCommitments
{
    /// <summary>Remaining commitments only: a mapped source PO replaces, rather than adds to, its canonical Supply line.</summary>
    public static decimal Remaining(FinanceState state, SupplyState supply, Guid jobId, DateOnly asOf)
    {
        var imported = state.ImportedCommitments.Where(c => c.JobId == jobId && c.Date <= asOf).ToArray();
        var bills = state.Bills.Where(b => (b.JournalId is not null || b.Status == "POSTED") && b.Date <= asOf).ToArray();
        var remaining = 0m;
        foreach (var order in supply.Orders.Where(o => FinanceMigrationService.IsCommitted(o.Status)))
        foreach (var line in order.Lines.Where(l => supply.Demands.Any(d => d.Id == l.DemandId && d.JobId == jobId)))
        {
            var snapshot = imported.SingleOrDefault(c => c.SupplyOrderId == order.Id && c.SupplyOrderLineId == line.Id);
            var billedLines = bills.Where(b => b.OrderId == order.Id).SelectMany(b => b.Lines).Where(l => l.OrderLineId == line.Id).ToArray();
            remaining += snapshot is null
                ? Math.Max(0, line.Quantity - billedLines.Sum(l => l.Quantity)) * line.UnitCost
                : Math.Max(0, snapshot.RemainingAmount - Math.Round(billedLines.Sum(l => l.Quantity) * (snapshot.UnitCost ?? line.UnitCost), 2, MidpointRounding.AwayFromZero));
        }
        foreach (var snapshot in imported.Where(c => c.SupplyOrderId is null))
        {
            // Explicit source IDs on bill lines avoid matching unrelated costs by Job/category/amount.
            var billed = bills.SelectMany(b => b.Lines).Where(l => l.ImportedCommitmentId == snapshot.Id).Sum(l => l.Amount);
            remaining += Math.Max(0, snapshot.RemainingAmount - billed);
        }
        return remaining;
    }

    public static void ValidateBill(FinanceState state, FinanceBill bill)
    {
        if (state.OpenItems.Any(i => i.Kind == "AP" && i.PartyId == bill.VendorId && i.Id != bill.Id
            && i.Number.Trim().Equals(bill.Number.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Duplicate vendor invoice number in posted or imported AP. Preserve the source bill and record an explicit correction.");
        foreach (var commitment in state.ImportedCommitments.Where(c => c.SupplyOrderId == bill.OrderId && c.SupplyOrderId.HasValue))
        {
            var linked = bill.Lines.Where(l => l.OrderLineId == commitment.SupplyOrderLineId).ToArray();
            if (linked.Any(l => l.JobId != commitment.JobId || l.Category != commitment.Category || bill.VendorId != commitment.VendorId || bill.Date < commitment.Date))
                throw new ArgumentException("Mapped source commitment must retain its canonical vendor, Job, category and cutover date.");
            if (commitment.UnitCost is decimal price)
            {
                var billed = state.Bills.Where(b => b.Id != bill.Id && b.OrderId == commitment.SupplyOrderId && (b.JournalId is not null || b.Status == "POSTED"))
                    .SelectMany(b => b.Lines).Where(l => l.OrderLineId == commitment.SupplyOrderLineId).Sum(l => l.Quantity);
                if (Math.Round((billed + linked.Sum(l => l.Quantity)) * price, 2, MidpointRounding.AwayFromZero) > commitment.RemainingAmount)
                    throw new ArgumentException("Bill exceeds the verified imported PO's unbilled quantity. Review/record the source amendment before billing.");
            }
        }
        foreach (var line in bill.Lines.Where(l => l.ImportedCommitmentId is not null))
        {
            var commitment = state.ImportedCommitments.SingleOrDefault(c => c.Id == line.ImportedCommitmentId)
                ?? throw new ArgumentException("Imported commitment is unavailable in this tenant.");
            if (commitment.SupplyOrderId.HasValue || line.OrderLineId.HasValue || bill.OrderId.HasValue)
                throw new ArgumentException("Use the canonical PO/line for a mapped commitment; never link both imported and operational sources.");
            if (line.JobId != commitment.JobId || bill.VendorId != commitment.VendorId || line.Category != commitment.Category || bill.Date < commitment.Date)
                throw new ArgumentException("Imported commitment bill must match its canonical vendor, Job, category and cutover date.");
            var already = state.Bills.Where(b => b.Id != bill.Id && (b.JournalId is not null || b.Status == "POSTED")).SelectMany(b => b.Lines).Where(l => l.ImportedCommitmentId == commitment.Id).Sum(l => l.Amount);
            var pending = bill.Lines.Where(l => l.ImportedCommitmentId == commitment.Id).Sum(l => l.Amount);
            if (already + pending > commitment.RemainingAmount) throw new ArgumentException("Bill exceeds the remaining imported commitment. Record/review the source amendment before billing.");
        }
    }
}
