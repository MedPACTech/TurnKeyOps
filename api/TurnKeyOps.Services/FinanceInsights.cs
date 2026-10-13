using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public sealed record FinanceForecastSource(string Kind,string Id,string Number,DateOnly Date,decimal Amount,Guid? JournalId,string Assumption);
public sealed record FinanceForecastDay(DateOnly Date,decimal RecordedCashMovement,decimal ReceiptsAssumed,decimal PaymentsAssumed,decimal ObligationsScenario,decimal DueDateScenario);
public sealed record FinanceCashForecast(DateOnly AsOf,DateOnly Through,decimal OpeningCash,decimal RecordedFutureMovement,decimal ReceiptsAssumed,decimal PaymentsAssumed,decimal ObligationsScenario,decimal DueDateScenario,decimal PendingBillsExcluded,string[] Assumptions,string[] Unknowns,FinanceForecastSource[] Sources,FinanceForecastDay[] Days);
public sealed record FinanceCommitmentSource(Guid OrderId,string Number,Guid OrderLineId,Guid DemandId,string Description,decimal Quantity,decimal BilledQuantity,decimal? UnbilledQuantity,decimal UnitCost,decimal Amount,string Status,DateTime? ExpectedAtUtc,string Basis,bool ImportedBaseline);
public sealed record FinanceFinding(string Severity,string Message,string[] SourceIds,string SuggestedAction);
public sealed record FinanceAnalysis(string Tool,DateOnly AsOf,string Title,string Summary,FinanceFinding[] Findings,string[] Limitations,FinanceCashForecast? Forecast=null);

// Pure, reproducible projections. No probabilistic collection claims or inferred missing costs.
public static class FinanceInsights
{
    public static FinanceCashForecast Forecast(FinanceState s,DateOnly asOf,int days=30)
    {
        if(days is <1 or >90||asOf==default||asOf.DayNumber>DateOnly.MaxValue.DayNumber-days)throw new ArgumentException("Forecast requires an as-of date and a 1–90 day horizon.");
        var end=asOf.AddDays(days);
        var cashCodes=s.CashAccounts.Select(a=>a.LedgerAccount).Distinct().ToHashSet();
        var opening=cashCodes.Sum(a=>FinanceLedger.AccountBalance(s,a,asOf));
        var sources=new List<FinanceForecastSource>();
        foreach(var j in s.Journals.Where(j=>j.Date>asOf&&j.Date<=end).OrderBy(j=>j.Date).ThenBy(j=>j.Id))
        {
            var amount=j.Lines.Where(l=>cashCodes.Contains(l.Account)).Sum(l=>l.Debit-l.Credit);
            if(amount!=0)sources.Add(new("posted-cash",j.Id.ToString(),j.Memo,j.Date,amount,j.Id,"Future-dated posted cash; not a new payment instruction."));
        }
        // Preserve signed opening credits as scenario offsets, never inferred cash refunds.
        var balances=s.OpenItems.Where(i=>i.Date<=asOf).Select(item=>new{Item=item,Balance=FinanceLedger.Balance(s,item,asOf)-s.Settlements.Where(t=>t.Date>asOf&&t.Date<=end).SelectMany(t=>t.Allocations.Select(a=>new{t.Kind,a.OpenItemId,a.Amount})).Where(a=>a.OpenItemId==item.Id).Sum(a=>a.Kind is "refund" or "vendor-refund"?-a.Amount:a.Amount)}).ToArray();
        var credits=balances.Where(i=>i.Balance<0).GroupBy(i=>(i.Item.Kind,i.Item.PartyId)).ToDictionary(g=>g.Key,g=>-g.Sum(i=>i.Balance));
        foreach(var credit in balances.Where(i=>i.Balance<0))sources.Add(new("unapplied-credit",credit.Item.Id.ToString(),credit.Item.Number,asOf.AddDays(1),0,credit.Item.JournalId,$"Recorded {credit.Item.Kind} credit {-credit.Balance:0.00} USD. Scenario assumes offset only against same-party due items; no cash refund is inferred. Apply the credit explicitly to confirm allocation."));
        foreach(var row in balances.Where(i=>i.Balance>0&&i.Item.DueDate<=end).OrderBy(i=>i.Item.DueDate).ThenBy(i=>i.Item.Id))
        {
            var item=row.Item;var balance=row.Balance;var key=(item.Kind,item.PartyId);var offset=Math.Min(balance,credits.GetValueOrDefault(key));credits[key]=credits.GetValueOrDefault(key)-offset;balance-=offset;
            var date=item.DueDate<=asOf?asOf.AddDays(1):item.DueDate;
            var assumption=item.Kind=="AR"?"Assumes collection on due date; overdue receipts assigned to day one. Collection is uncertain.":"Assumes payment on due date; overdue obligations assigned to day one. This does not send funds.";
            if(offset>0)assumption+=$" Assumed same-party credit offset {offset:0.00} USD; allocation requires owner review.";
            sources.Add(new(item.Kind,item.Id.ToString(),item.Number,date,item.Kind=="AR"?balance:-balance,item.JournalId,assumption));
        }
        decimal runningKnown=opening,runningDue=opening;
        var path=new List<FinanceForecastDay>();
        for(var d=asOf.AddDays(1);d<=end;d=d.AddDays(1))
        {
            var rows=sources.Where(s=>s.Date==d).ToArray();
            var recorded=rows.Where(s=>s.Kind=="posted-cash").Sum(s=>s.Amount);
            var receipts=rows.Where(s=>s.Kind=="AR").Sum(s=>s.Amount);
            var payments=-rows.Where(s=>s.Kind=="AP").Sum(s=>s.Amount);
            runningKnown+=recorded-payments;runningDue+=recorded+receipts-payments;
            path.Add(new(d,recorded,receipts,payments,runningKnown,runningDue));
            if(d==end)break;
        }
        var pending=s.Bills.Where(b=>b.Status is "DRAFT" or "PENDING_APPROVAL").Sum(b=>b.Total);
        return new(asOf,end,opening,sources.Where(s=>s.Kind=="posted-cash").Sum(s=>s.Amount),sources.Where(s=>s.Kind=="AR").Sum(s=>s.Amount),-sources.Where(s=>s.Kind=="AP").Sum(s=>s.Amount),runningKnown,runningDue,pending,
            ["USD; posted cash across registered bank/cash ledger accounts, with internal transfers netted.","Two scenarios: recorded obligations excludes assumed AR collections; due-date scenario adds them. Neither is a guaranteed cash balance.","Only open AR/AP recorded by the as-of date are scheduled. Already-posted future allocations are reserved to avoid double counting.","Unapplied credits offset same-party due items in the scenario only; credit allocation and refund timing are not confirmed."],
            ["Unrecorded payroll, tax payments, overhead, unbilled costs and future sales are unknown and excluded.","Open purchase commitments have no authoritative cash payment schedule and are excluded; review Job commitments separately.", $"Unapproved/draft bills excluded: {pending:0.00} USD. Invoice events not yet reconciled into Finance are excluded."],sources.ToArray(),path.ToArray());
    }

    public static FinanceCommitmentSource[] Commitments(FinanceState s,SupplyState supply,Guid job,DateOnly asOf) =>
        supply.Orders.Where(o=>o.Status is "APPROVED" or "SENT" or "ACKNOWLEDGED" or "PARTIALLY_RECEIVED" or "RECEIVED")
        .SelectMany(o=>o.Lines.Where(l=>supply.Demands.Any(d=>d.Id==l.DemandId&&d.JobId==job)).Select(l=>{
            var posted=s.Bills.Where(b=>b.OrderId==o.Id&&(b.JournalId is not null||b.Status=="POSTED")&&b.Date<=asOf).SelectMany(b=>b.Lines).Where(b=>b.OrderLineId==l.Id).ToArray();
            var billed=posted.Sum(b=>b.Quantity);var remaining=Math.Max(0,l.Quantity-billed);
            var snapshot=s.ImportedCommitments.SingleOrDefault(c=>c.JobId==job&&c.SupplyOrderId==o.Id&&c.SupplyOrderLineId==l.Id&&c.Date<=asOf);
            var committedPrice=snapshot?.UnitCost??l.UnitCost;
            var amount=snapshot is null?remaining*l.UnitCost:Math.Max(0,snapshot.RemainingAmount-Math.Round(posted.Sum(b=>b.Quantity)*committedPrice,2,MidpointRounding.AwayFromZero));
            return new FinanceCommitmentSource(o.Id,o.Number,l.Id,l.DemandId,l.Description,l.Quantity,billed,snapshot is null?remaining:null,committedPrice,Math.Round(amount,2,MidpointRounding.AwayFromZero),o.Status,o.ExpectedAtUtc,snapshot is null?"Unbilled PO quantity at unit price.":"Imported remaining-value baseline less billed quantity at frozen cutover PO unit price; original PO quantity is not the imported remaining quantity.",snapshot is not null);
        })).OrderBy(o=>o.Number).ThenBy(o=>o.OrderLineId).ToArray();

    public static FinanceAnalysis Review(FinanceState s,SupplyState supply,string tool,DateOnly asOf)
    {
        if(asOf==default||asOf.DayNumber>DateOnly.MaxValue.DayNumber-30)throw new ArgumentException("Use an as-of date with a supported 30-day horizon.");
        var findings=new List<FinanceFinding>();
        var items=s.OpenItems.Where(i=>i.Date<=asOf).Select(i=>new{Item=i,Balance=FinanceLedger.Balance(s,i,asOf)}).Where(i=>i.Balance>0).ToArray();
        var title=tool switch{"finance.ar"=>"Customer collections review","finance.ap"=>"Vendor obligations review","finance.job-health"=>"Job cost and profitability review","finance.cash"=>"30-day cash scenario review","finance.close-readiness"=>"Financial close review",_=>throw new ArgumentException("Unsupported Finance analysis tool.")};
        if(tool is "finance.ar" or "finance.close-readiness")
            foreach(var g in items.Where(x=>x.Item.Kind=="AR"&&x.Item.DueDate<asOf).GroupBy(x=>x.Item.PartyId).OrderByDescending(g=>g.Sum(x=>x.Balance)))
                findings.Add(new("attention",$"Customer {g.Key}: {g.Count()} overdue invoice(s), {g.Sum(x=>x.Balance):0.00} USD outstanding.",g.Select(x=>$"open-item:{x.Item.Id}").ToArray(),"Review source invoices and collection history before drafting a reminder. No message has been sent."));
        if(tool is "finance.ap" or "finance.close-readiness")
        {
            foreach(var g in items.Where(x=>x.Item.Kind=="AP"&&x.Item.DueDate<=asOf.AddDays(30)).GroupBy(x=>x.Item.PartyId).OrderByDescending(g=>g.Sum(x=>x.Balance)))
                findings.Add(new("attention",$"Vendor {g.Key}: {g.Sum(x=>x.Balance):0.00} USD due or overdue within 30 days.",g.Select(x=>$"open-item:{x.Item.Id}").ToArray(),"Review due dates and cash scenarios before recording an externally completed payment."));
            foreach(var b in s.Bills.Where(b=>b.Status=="PENDING_APPROVAL"))findings.Add(new("review",$"Bill {(b.Number.Length>80?b.Number[..80]+"…":b.Number)} requires approval; {b.MatchExceptions.Length} matching exception(s).",[$"bill:{b.Id}"],"Review PO/receipt evidence and exceptions. Bob cannot approve or pay this bill."));
        }
        if(tool is "finance.job-health" or "finance.close-readiness")
            foreach(var b in s.Budgets)
            {
                var actual=s.Costs.Where(c=>c.JobId==b.JobId&&c.Date<=asOf).Sum(c=>c.Amount);
                var revenue=s.Journals.Where(j=>j.Date<=asOf).SelectMany(j=>j.Lines).Where(l=>l.JobId==b.JobId&&s.Accounts.Any(a=>a.Code==l.Account&&a.Type is "REVENUE" or "OTHER_INCOME")).Sum(l=>l.Credit-l.Debit);
                var commitments=Commitments(s,supply,b.JobId,asOf).Where(c=>c.Amount>0).ToArray();
                if(commitments.Length>0)findings.Add(new("recorded",$"Job {b.JobId}: {commitments.Sum(c=>c.Amount):0.00} USD remaining purchase commitments; these are separate from incurred actual costs.",commitments.Select(c=>$"purchase-order:{c.OrderId}").Distinct().ToArray(),"Review PO quantities, approved bills and receipts. PO tax/shipping without Job allocation and payment timing are not inferred."));
                var imported=s.ImportedCommitments.Where(c=>c.JobId==b.JobId&&c.Date<=asOf&&c.SupplyOrderId is null).Sum(c=>Math.Max(0,c.RemainingAmount-s.Bills.Where(b=>(b.JournalId is not null||b.Status=="POSTED")&&b.Date<=asOf).SelectMany(b=>b.Lines).Where(l=>l.ImportedCommitmentId==c.Id).Sum(l=>l.Amount)));
                if(imported>0)findings.Add(new("recorded",$"Job {b.JobId}: {imported:0.00} USD remaining imported commitments, separate from live PO commitments and actual costs.",s.ImportedCommitments.Where(c=>c.JobId==b.JobId&&c.Date<=asOf&&c.SupplyOrderId is null).Select(c=>$"imported-commitment:{c.Id}").ToArray(),"Review preserved migration sources and linked bills. The opening import does not create another actual expense."));
                var extrasOutstanding=FinanceBillExtraAllocations.Outstanding(s,b.JobId,asOf);
                if(extrasOutstanding)findings.Add(new("unknown",$"Job {b.JobId}: posted bill tax or shipping remains unallocated; actual Job costs may be understated.",[$"job:{b.JobId}"],"An owner must review and allocate the existing bill expense to Jobs and cost categories before interpreting profitability."));
                var complete=!extrasOutstanding&&b.TaxBasisKnown&&b.ActualsComplete&&b.Categories.Values.All(v=>v.HasValue)&&!s.Costs.Any(c=>c.JobId==b.JobId&&c.Date<=asOf&&c.SourceType=="migration-job-cost");
                findings.Add(new(complete&&revenue<actual?"attention":complete?"recorded":"unknown",complete?$"Job {b.JobId}: recorded revenue {revenue:0.00} USD less actual costs {actual:0.00} USD = {revenue-actual:0.00} USD gross profit.":$"Job {b.JobId}: profitability is unknown. Cost completeness, budget categories, tax basis or historical revenue matching still require review.",[$"job:{b.JobId}",..s.Costs.Where(c=>c.JobId==b.JobId&&c.Date<=asOf).Select(c=>$"cost:{c.Id}")],complete?"Inspect source costs and remaining purchase commitments before interpreting margin as final.":"Review missing cost categories and source evidence; do not infer a margin."));
            }
        if(tool=="finance.close-readiness")
            foreach(var p in s.Periods.Where(p=>p.Status=="OPEN"))foreach(var issue in FinanceLedger.CloseIssues(s,p))findings.Add(new("blocking",$"{p.Id}: {issue}",[$"period:{p.Id}"],"Resolve the source exception before an authorized owner closes the period."));
        FinanceCashForecast? forecast=null;
        if(tool=="finance.cash")
        {
            forecast=Forecast(s,asOf);
            findings.Add(new(forecast.ObligationsScenario<0?"attention":"scenario",$"30-day cash: {forecast.OpeningCash:0.00} USD posted now; {forecast.ObligationsScenario:0.00} USD with recorded obligations and future posted movement; {forecast.DueDateScenario:0.00} USD if assumed due-date receipts arrive.",forecast.Sources.Select(s=>$"{s.Kind}:{s.Id}").ToArray(),"Review every forecast source and timing assumption. Missing cash expenses and collection uncertainty prevent treating this as available spending."));
        }
        var summary=findings.Count==0?"No recorded exceptions found for this review. Missing or unreconciled source data can still affect the result.":$"{findings.Count} source-backed finding(s). {findings.Count(f=>f.Severity=="unknown")} require missing-data review; {findings.Count(f=>f.Severity=="blocking")} block close.";
        var shown=findings.OrderBy(f=>f.Severity=="blocking"?0:f.Severity=="attention"?1:f.Severity=="unknown"?2:3).Take(5).Select(f=>f with{SourceIds=f.SourceIds.Take(5).ToArray(),SuggestedAction=f.SuggestedAction+(f.SourceIds.Length>5?$" Showing 5 of {f.SourceIds.Length} source references; the Finance view contains the complete records.":"")}).ToArray();
        if(findings.Count>shown.Length)summary+=$" Showing the first {shown.Length}; review the corresponding Finance view for all records.";
        var limits=new List<string>{"Uses tenant Finance records and the selected as-of date. No external messages, payments or ledger postings were made.","Unposted/unreconciled operational events and missing costs are excluded. Profit is accounting performance, not cash availability."};
        if(forecast is not null){limits.AddRange(forecast.Assumptions);limits.AddRange(forecast.Unknowns);}
        // The complete forecast is in the guarded workspace. Keep Bob's stored explanation within its audited response budget.
        return new(tool,asOf,title,summary,shown,limits.ToArray());
    }
}
