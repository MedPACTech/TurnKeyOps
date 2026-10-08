using System.Security.Cryptography;
using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public static class FinanceLedger
{
    public static readonly string[] Categories = ["MATERIAL","LABOR","EQUIPMENT","SUBCONTRACT","OTHER_DIRECT_COST","BURDEN","OVERHEAD_ALLOCATED"];
    public static readonly string[] AccountTypes = ["ASSET","LIABILITY","EQUITY","REVENUE","EXPENSE","COST_OF_GOODS_SOLD","OTHER_INCOME","OTHER_EXPENSE"];
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JobConfigurationService.Json)));
    public static decimal Money(decimal value)
    {
        if (value != Math.Round(value,2) || Math.Abs(value)>1_000_000_000_000m) throw new ArgumentException("Use currency amounts with at most two decimal places within the supported range.");
        return value;
    }
    public static void Positive(decimal value) { Money(value); if(value<=0)throw new ArgumentException("Amount must be positive."); }
    public static string Required(string value) => string.IsNullOrWhiteSpace(value)||value.Length>1000?throw new ArgumentException("A non-empty value of at most 1000 characters is required."):value.Trim();
    public static void Initialize(FinanceState state)
    {
        if(state.Accounts.Count>0)throw new ArgumentException("Finance is already initialized.");
        state.Accounts.AddRange([
            new("1000","Operating cash","ASSET","DEBIT",true),new("1100","Accounts receivable","ASSET","DEBIT",true),
            new("1200","Inventory","ASSET","DEBIT",true),new("2000","Accounts payable","LIABILITY","CREDIT",true),
            new("2100","Sales tax payable","LIABILITY","CREDIT",true),new("3000","Opening equity / retained earnings","EQUITY","CREDIT",true),
            new("4000","Contract revenue","REVENUE","CREDIT",true),new("5000","Direct job costs","COST_OF_GOODS_SOLD","DEBIT",true),
            new("6000","Operating expenses","EXPENSE","DEBIT")]);
        state.CostCodes.AddRange(Categories.Select(c=>new FinanceCostCode(c,c.Replace('_',' '),c)));
        state.CashAccounts.Add(new("operating","Operating cash","1000","",""));
    }
    public static FinanceJournal Post(FinanceState state,DateOnly date,string type,string source,string memo,FinanceLine[] lines,string actor,Guid? reverses=null)
    {
        Required(source); Required(actor); Required(memo);
        // Compare content before period checks: a retry of committed history remains a no-op after close.
        var fingerprint=Hash(new{date,type,source,memo,lines,reverses});
        var existing=state.Journals.SingleOrDefault(j=>j.SourceType==type&&j.SourceId==source);
        if(existing is not null){if(existing.Fingerprint!=fingerprint)throw new ArgumentException("Source key was already posted with different content.");return existing;}
        if(date==default)throw new ArgumentException("Accounting date is required.");
        var period=state.Periods.SingleOrDefault(p=>date>=p.Start&&date<=p.End)??throw new ArgumentException("Create the accounting period before posting.");
        if(period.Status!="OPEN")throw new ArgumentException("This accounting period is closed to posting.");
        if(lines.Length is <2 or >500 || lines.Sum(l=>l.Debit)!=lines.Sum(l=>l.Credit) || lines.Sum(l=>l.Debit)<=0)throw new ArgumentException("A journal requires balanced, positive debits and credits.");
        foreach(var line in lines)
        {
            Money(line.Debit);Money(line.Credit);
            if(line.Debit<0||line.Credit<0||(line.Debit==0)==(line.Credit==0))throw new ArgumentException("Each line requires exactly one positive debit or credit.");
            if(!state.Accounts.Any(a=>a.Code==line.Account&&a.Active))throw new ArgumentException("Journal account is missing or inactive.");
            if(line.CostCode is not null&&!state.CostCodes.Any(c=>c.Code==line.CostCode&&c.Active))throw new ArgumentException("Cost code is missing or inactive.");
        }
        if(lines.Any(l=>state.CashAccounts.Any(a=>a.LedgerAccount==l.Account&&state.Reconciliations.Any(r=>r.CashAccountId==a.Id&&date<=r.End))))throw new ArgumentException("Cash activity cannot change a reconciled statement interval.");
        var journal=new FinanceJournal(Guid.NewGuid(),date,period.Id,type,source,memo,"POSTED",actor,actor,DateTime.UtcNow,lines.ToArray(),fingerprint,reverses);
        state.Journals.Add(journal);return journal;
    }
    public static decimal Balance(FinanceState state,FinanceOpenItem item,DateOnly asOf) => item.Date>asOf?0:item.Total-state.Settlements.Where(s=>s.Date<=asOf).SelectMany(s=>s.Allocations.Select(a=>new{ s.Kind,a.OpenItemId,a.Amount })).Where(a=>a.OpenItemId==item.Id).Sum(a=>a.Kind is "refund" or "vendor-refund"?-a.Amount:a.Amount);
    public static decimal AccountBalance(FinanceState state,string code,DateOnly asOf) => state.Journals.Where(j=>j.Date<=asOf).SelectMany(j=>j.Lines).Where(l=>l.Account==code).Sum(l=>l.Debit-l.Credit);
    public static object Reports(FinanceState s,DateOnly from,DateOnly asOf)
    {
        var lines=s.Journals.Where(j=>j.Date<=asOf).SelectMany(j=>j.Lines).ToArray();
        var trial=s.Accounts.Select(a=>new{a.Code,a.Name,a.Type,Debit=lines.Where(l=>l.Account==a.Code).Sum(l=>l.Debit),Credit=lines.Where(l=>l.Account==a.Code).Sum(l=>l.Credit),Balance=AccountBalance(s,a.Code,asOf)}).ToArray();
        var incomeTypes=new[]{"REVENUE","OTHER_INCOME","EXPENSE","COST_OF_GOODS_SOLD","OTHER_EXPENSE"};
        var activity=s.Journals.Where(j=>j.Date>=from&&j.Date<=asOf).SelectMany(j=>j.Lines).ToArray();
        var income=s.Accounts.Where(a=>incomeTypes.Contains(a.Type)).Select(a=>new{a.Code,a.Name,a.Type,Amount=activity.Where(l=>l.Account==a.Code).Sum(l=>l.Credit-l.Debit)}).ToArray();
        var netIncome=trial.Where(a=>incomeTypes.Contains(a.Type)).Sum(a=>-a.Balance);
        var assets=trial.Where(a=>a.Type=="ASSET").Sum(a=>a.Balance);
        var liabilities=trial.Where(a=>a.Type=="LIABILITY").Sum(a=>-a.Balance);
        var equity=trial.Where(a=>a.Type=="EQUITY").Sum(a=>-a.Balance)+netIncome;
        var aging=s.OpenItems.Where(i=>i.Date<=asOf).Select(i=>new{ i.Id,i.Kind,i.Number,i.PartyId,i.JobId,i.DueDate,Balance=Balance(s,i,asOf),DaysPastDue=Math.Max(0,asOf.DayNumber-i.DueDate.DayNumber),Bucket=asOf<=i.DueDate?"Current":asOf.DayNumber-i.DueDate.DayNumber<=30?"1–30":asOf.DayNumber-i.DueDate.DayNumber<=60?"31–60":asOf.DayNumber-i.DueDate.DayNumber<=90?"61–90":"90+" }).ToArray();
        var ar=aging.Where(i=>i.Kind=="AR").Sum(i=>i.Balance);var ap=aging.Where(i=>i.Kind=="AP").Sum(i=>i.Balance);
        return new{From=from,AsOf=asOf,TrialBalance=trial,TrialDifference=trial.Sum(a=>a.Balance),IncomeStatement=income,Profit=income.Sum(a=>a.Amount),BalanceSheet=new{Assets=assets,Liabilities=liabilities,Equity=equity,CurrentEarnings=netIncome,Difference=assets-liabilities-equity},Aging=aging,Ar=ar,Ap=ap,ArDifference=AccountBalance(s,s.Policy.Accounts["ar"],asOf)-ar,ApDifference=-AccountBalance(s,s.Policy.Accounts["ap"],asOf)-ap,Cash=s.CashAccounts.Select(c=>new{c.Id,c.Name,Balance=AccountBalance(s,c.LedgerAccount,asOf)}).ToArray()};
    }
    public static string[] CloseIssues(FinanceState s,FinancePeriod period)
    {
        var issues=new List<string>();
        if(s.JournalDrafts.Any(d=>d.Date<=period.End&&d.PostedJournalId is null))issues.Add("Unposted journal drafts require review.");
        if(s.Bills.Any(b=>b.Date<=period.End&&b.JournalId is null&&b.Status!="VOID"))issues.Add("Unposted vendor bills require review.");
        if(s.BankTransactions.Any(b=>b.Date<=period.End&&b.ReconciliationId is null))issues.Add("Imported bank activity is not reconciled.");
        if(s.OpenItems.Where(i=>i.Kind=="AR").Sum(i=>Balance(s,i,period.End))!=AccountBalance(s,s.Policy.Accounts["ar"],period.End))issues.Add("AR control does not reconcile.");
        if(s.OpenItems.Where(i=>i.Kind=="AP").Sum(i=>Balance(s,i,period.End))!=-AccountBalance(s,s.Policy.Accounts["ap"],period.End))issues.Add("AP control does not reconcile.");
        return issues.ToArray();
    }
    public static FinanceJournal Settle(FinanceState s,FinanceCommand c,string actor,string kind)
    {
        Positive(c.Amount);Required(c.SourceKey);Required(c.Reference);
        var previous=s.Settlements.SingleOrDefault(p=>p.Kind==kind&&p.SourceKey==c.SourceKey);
        if(previous is not null){if(previous.PartyId!=c.PartyId||previous.Amount!=c.Amount||previous.Date!=c.Date||previous.CashAccount!=c.Account||previous.Reference!=c.Reference||Hash(previous.Allocations)!=Hash(c.Allocations))throw new ArgumentException("Settlement source key conflicts with recorded content.");return s.Journals.Single(j=>j.Id==previous.JournalId);}
        if(c.PartyId==Guid.Empty||c.Allocations.Length==0||c.Allocations.Sum(a=>a.Amount)!=c.Amount||c.Allocations.Select(a=>a.OpenItemId).Distinct().Count()!=c.Allocations.Length)throw new ArgumentException("Allocate the entire amount once to each item for a single party.");
        var ap=kind is "vendor-payment" or "vendor-credit" or "vendor-refund";var refund=kind is "refund" or "vendor-refund";var credit=kind is "credit" or "vendor-credit";
        foreach(var allocation in c.Allocations)
        {
            Positive(allocation.Amount);
            var item=s.OpenItems.SingleOrDefault(i=>i.Id==allocation.OpenItemId&&i.PartyId==c.PartyId&&i.Kind==(ap?"AP":"AR"))??throw new ArgumentException("Allocation item is not available for this party.");
            if(c.Date<item.Date)throw new ArgumentException("Settlement cannot predate its source item.");
            if(!refund&&allocation.Amount>Balance(s,item,DateOnly.MaxValue))throw new ArgumentException("Allocation exceeds the outstanding balance.");
            if(refund){var paid=s.Settlements.Where(p=>p.Kind==(ap?"vendor-payment":"payment")).SelectMany(p=>p.Allocations).Where(a=>a.OpenItemId==item.Id).Sum(a=>a.Amount);var refunded=s.Settlements.Where(p=>p.Kind==kind).SelectMany(p=>p.Allocations).Where(a=>a.OpenItemId==item.Id).Sum(a=>a.Amount);if(allocation.Amount>paid-refunded)throw new ArgumentException("Refund exceeds recorded payments.");}
        }
        var control=s.Policy.Accounts[ap?"ap":"ar"];
        var offset=credit?s.Policy.Accounts[ap?"cost":"revenue"]:s.CashAccounts.SingleOrDefault(a=>a.Id==c.Account&&a.Active)?.LedgerAccount??throw new ArgumentException("Select an active cash account.");
        var controlDebit=ap!=refund;
        FinanceLine[] lines=[new(control,controlDebit?c.Amount:0,controlDebit?0:c.Amount,CustomerId:ap?null:c.PartyId,VendorId:ap?c.PartyId:null),new(offset,controlDebit?0:c.Amount,controlDebit?c.Amount:0)];
        if(credit)
        {
            var creditLines=new List<FinanceLine>{lines[0]};
            foreach(var allocation in c.Allocations)
            {
                var item=s.OpenItems.Single(i=>i.Id==allocation.OpenItemId);var source=s.Journals.Single(j=>j.Id==item.JournalId);
                if(source.SourceType is not ("invoice" or "vendor-bill"))throw new ArgumentException("Imported open-item credits require original revenue/tax/cost coding; do not infer it from opening balances.");
                var offsets=source.Lines.Where(l=>l.Account!=control).ToArray();var baseTotal=offsets.Sum(l=>ap?l.Debit:l.Credit);
                var exact=offsets.Select(l=>allocation.Amount*(ap?l.Debit:l.Credit)/baseTotal).ToArray();var amounts=exact.Select(a=>decimal.Floor(a*100)/100).ToArray();
                var cents=(int)((allocation.Amount-amounts.Sum())*100);
                foreach(var index in Enumerable.Range(0,offsets.Length).OrderByDescending(i=>exact[i]-amounts[i]).ThenBy(i=>i).Take(cents))amounts[index]+=0.01m;
                for(var index=0;index<offsets.Length;index++)if(amounts[index]>0)creditLines.Add(offsets[index] with{Debit=ap?0:amounts[index],Credit=ap?amounts[index]:0});
            }
            lines=creditLines.ToArray();
        }
        var journal=Post(s,c.Date,kind,c.SourceKey,c.Reason.Length>0?c.Reason:c.Reference,lines,actor);
        if(kind=="vendor-credit")foreach(var line in lines.Where(l=>l.JobId is not null&&l.Credit>0))s.Costs.Add(new(Guid.NewGuid(),line.JobId!.Value,line.Category??"OTHER_DIRECT_COST",line.CostCode,-line.Credit,c.Date,"vendor-credit",c.SourceKey,journal.Id));
        s.Settlements.Add(new(c.Id,kind,c.PartyId,c.Date,c.Amount,c.Account,c.Method,c.Reference,c.SourceKey,journal.Id,c.Allocations.ToArray()));
        return journal;
    }
    public static string[] Match(FinanceBill bill,SupplyState supply,IEnumerable<FinanceBill> existing)
    {
        var issues=new List<string>();
        if(existing.Any(b=>b.Id!=bill.Id&&b.VendorId==bill.VendorId&&b.Number.Trim().Equals(bill.Number.Trim(),StringComparison.OrdinalIgnoreCase)&&b.Status!="VOID"))issues.Add("Duplicate vendor invoice number.");
        if(bill.OrderId is not null)
        {
            var order=supply.Orders.SingleOrDefault(o=>o.Id==bill.OrderId&&o.VendorId==bill.VendorId)??throw new ArgumentException("PO is unavailable for this vendor.");
            if(bill.Total>order.Total)issues.Add("Bill exceeds PO total.");
            foreach(var group in bill.Lines.GroupBy(l=>l.OrderLineId))
            {
                var line=order.Lines.SingleOrDefault(l=>l.Id==group.Key);
                if(line is null){issues.Add("Bill line has no matching PO line.");continue;}
                var prior=existing.Where(b=>b.Id!=bill.Id&&b.OrderId==bill.OrderId&&b.Status!="VOID").SelectMany(b=>b.Lines).Where(l=>l.OrderLineId==line.Id).Sum(l=>l.Quantity);
                if(group.Sum(l=>l.Quantity)+prior>line.Quantity)issues.Add("Billed quantity exceeds PO quantity.");
                if(group.Any(l=>l.UnitCost!=line.UnitCost))issues.Add("Unit cost variance.");
                var received=supply.Receipts.Where(r=>r.OrderId==order.Id&&r.LineId==line.Id).Sum(r=>r.Accepted);
                if(received<group.Sum(l=>l.Quantity)+prior)issues.Add("Missing or insufficient receipt.");
            }
            if(bill.ReceiptIds.Any(id=>!supply.Receipts.Any(r=>r.Id==id&&r.OrderId==order.Id)))throw new ArgumentException("Receipt does not belong to the PO.");
        }
        return issues.Distinct().ToArray();
    }
}
