using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Services;

/// <summary>One deterministic posting path for explicit reconciliation and durable dispatch.</summary>
public static class FinanceInvoicePosting
{
    public static DateOnly Cutover(FinanceState state) => state.MigrationBatches.Select(b => b.CutoverDate)
        .Concat(state.Journals.Where(j => j.SourceType == "opening-import").Select(j => j.Date)).DefaultIfEmpty(DateOnly.MinValue).Max();

    public static void Apply(FinanceState state, InvoiceDto invoice, string actor)
    {
        if (invoice.Status == InvoiceStatus.Draft) return;
        if (state.Construction.Contracts.Any(c=>c.JobId==invoice.JobId)) throw new ArgumentException("Construction Jobs require progress billing through pay applications.");
        if (invoice.Status == InvoiceStatus.Void)
        {
            if (state.OpenItems.Any(i => i.Id == invoice.Id && i.Kind == "AR"))
                throw new ArgumentException("A posted invoice was voided upstream; record an authorized credit before reconciliation.");
            return;
        }
        FinanceLedger.Positive(invoice.Total);
        FinanceLedger.Money(invoice.Subtotal); FinanceLedger.Money(invoice.TaxAmount);
        if (invoice.Id == Guid.Empty || invoice.CustomerId == Guid.Empty || invoice.Subtotal < 0 || invoice.TaxAmount < 0 || invoice.Subtotal + invoice.TaxAmount != invoice.Total)
            throw new ArgumentException("Invoice totals or canonical Customer do not reconcile.");
        var cutover = Cutover(state);
        var issueDate = DateOnly.FromDateTime(invoice.IssueDate);
        var historical = issueDate <= cutover;
        var openItem = state.OpenItems.SingleOrDefault(i => i.Id == invoice.Id && i.Kind == "AR");
        if (openItem is not null && (openItem.PartyId != invoice.CustomerId || openItem.JobId != invoice.JobId))
            throw new ArgumentException("Invoice and imported AR identity do not match.");
        if (historical && openItem is null)
        {
            var historicalCash = invoice.Payments.Where(p => p.Status == "succeeded" && DateOnly.FromDateTime(p.OccurredAtUtc) <= cutover)
                .Sum(p => p.Kind == "refund" ? -p.Amount : p.Amount);
            if (invoice.Total > historicalCash)
                throw new ArgumentException("Historical invoice has an unreconciled balance but no canonical imported AR item. Review the cutover source mapping; do not omit or repost historical revenue.");
        }
        if (!historical)
        {
            var lines = new List<FinanceLine> { new(state.Policy.Accounts["ar"], invoice.Total, 0, invoice.JobId, CustomerId: invoice.CustomerId) };
            if (invoice.Subtotal > 0) lines.Add(new(state.Policy.Accounts["revenue"], 0, invoice.Subtotal, invoice.JobId, CustomerId: invoice.CustomerId));
            if (invoice.TaxAmount > 0) lines.Add(new(state.Policy.Accounts["tax"], 0, invoice.TaxAmount, invoice.JobId, CustomerId: invoice.CustomerId));
            var journal = FinanceLedger.Post(state, issueDate, "invoice", invoice.Id.ToString(), invoice.InvoiceNumber, lines.ToArray(), actor);
            if (openItem is null)
            {
                openItem = new(invoice.Id, "AR", invoice.CustomerId, invoice.InvoiceNumber, issueDate, DateOnly.FromDateTime(invoice.DueDate), invoice.Total, journal.Id, invoice.JobId, invoice.EstimateId?.ToString());
                state.OpenItems.Add(openItem);
            }
        }
        foreach (var payment in invoice.Payments.Where(p => p.Status == "succeeded").OrderBy(p => p.OccurredAtUtc).ThenBy(p => p.Id))
        {
            if (payment.FinanceSettlementId is Guid settlementId)
            {
                if (!state.Settlements.Any(p => p.Id == settlementId && p.Allocations.Any(a => a.OpenItemId == invoice.Id && a.Amount == payment.Amount)))
                    throw new ArgumentException("Finance settlement provenance does not match the invoice.");
                continue;
            }
            var date = DateOnly.FromDateTime(payment.OccurredAtUtc);
            if (date <= cutover) continue; // Opening AR already represents these historical events.
            if (openItem is null) throw new ArgumentException("Post-cutover activity needs the canonical imported invoice AR item.");
            if (payment.Kind is not ("payment" or "refund")) throw new ArgumentException("Unsupported invoice payment event kind.");
            FinanceLedger.Positive(payment.Amount); FinanceLedger.Required(payment.IdempotencyKey);
            if (payment.Id == Guid.Empty) throw new ArgumentException("Invoice payment needs a stable event identity.");
            var refund = payment.Kind == "refund";
            var key = $"{invoice.Id}:{payment.IdempotencyKey}";
            var settlement = state.Settlements.SingleOrDefault(p => p.SourceKey == key && p.Kind == payment.Kind);
            FinanceLine[] paymentLines = [new(state.Policy.Accounts["cash"], refund ? 0 : payment.Amount, refund ? payment.Amount : 0), new(state.Policy.Accounts["ar"], refund ? payment.Amount : 0, refund ? 0 : payment.Amount, invoice.JobId, CustomerId: invoice.CustomerId)];
            var memo = string.IsNullOrWhiteSpace(payment.ExternalReference) ? key : payment.ExternalReference;
            var posted = FinanceLedger.Post(state, date, payment.Kind, key, memo, paymentLines, actor);
            if (settlement is null) state.Settlements.Add(new(payment.Id, payment.Kind, invoice.CustomerId, date, payment.Amount, "operating", payment.Method, payment.ExternalReference ?? "", key, posted.Id, [new(invoice.Id, payment.Amount)], Origin: "invoice-event"));
        }
    }
}
