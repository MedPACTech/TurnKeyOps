using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class FinanceApRetainage
{
    public static decimal Held(FinanceState s,FinanceBill bill,DateOnly date)=>bill.Date>date?0:bill.RetainageAmount-s.Operations.ApRetainageEvents.Where(e=>e.BillId==bill.Id&&e.Date<=date).Sum(e=>e.RetainedAmount);
    public static void ValidateBill(FinanceState s,FinanceBill bill)
    {
        if(bill.RetainagePercent<0||bill.RetainagePercent>100||bill.RetainagePercent!=Math.Round(bill.RetainagePercent,2))throw new ArgumentException("AP retainage percentage must be between zero and 100 with at most two decimals.");
        if(bill.RetainageAmount>0)Account(s);
    }
    public static string Account(FinanceState s)
    {
        var code=s.Operations.ApRetainageAccount;
        if(!s.Accounts.Any(a=>a.Code==code&&a.Active&&a.Type=="LIABILITY")||s.Policy.Accounts.Values.Contains(code)||s.Construction.Accounts.Values.Contains(code))throw new ArgumentException("Configure a dedicated AP retainage payable account first.");return code;
    }
    public static void Apply(FinanceState s,FinanceCommand c,string actor,bool credit)
    {
        FinanceLedger.Required(c.SourceKey);FinanceLedger.Required(c.Reason);FinanceLedger.Positive(c.Amount);
        var old=s.Operations.ApRetainageEvents.SingleOrDefault(e=>e.SourceKey==c.SourceKey);
        if(old is not null)throw new ArgumentException("AP retainage source key already exists.");
        var bill=s.Bills.SingleOrDefault(b=>b.Id.ToString()==c.Target&&b.JournalId is not null&&b.RetainageAmount>0)??throw new ArgumentException("Posted retained vendor bill required.");
        if(c.Date<bill.Date)throw new ArgumentException("Retainage action cannot predate its bill.");
        var held=Held(s,bill,DateOnly.MaxValue);var events=s.Operations.ApRetainageEvents.Where(e=>e.BillId==bill.Id).ToArray();
        if(events.Any(e=>e.Date>c.Date))throw new ArgumentException("AP retainage corrections must follow existing retained-bill events chronologically.");
        var allocations=new List<FinanceAllocation>();decimal retained;decimal ap;
        if(!credit){retained=c.Amount;ap=c.Amount;if(retained>held)throw new ArgumentException("Release exceeds retained payable balance.");}
        else
        {
            var uncredited=bill.Total-events.Where(e=>e.Kind=="CREDIT").Sum(e=>e.GrossAmount);
            if(c.Amount>uncredited)throw new ArgumentException("Credit exceeds uncredited gross bill.");
            retained=c.Amount==uncredited?held:Math.Min(held,Math.Round(c.Amount*bill.RetainagePercent/100,2,MidpointRounding.AwayFromZero));ap=c.Amount-retained;
            var itemIds=events.Where(e=>e.Kind=="RELEASE").Select(e=>e.Id).Append(bill.Id).ToHashSet();var remaining=ap;
            foreach(var item in s.OpenItems.Where(i=>itemIds.Contains(i.Id)).OrderBy(i=>i.Date).ThenBy(i=>i.Id))
            {var amount=Math.Min(remaining,Math.Max(0,FinanceLedger.Balance(s,item,DateOnly.MaxValue)));if(amount>0){allocations.Add(new(item.Id,amount));remaining-=amount;}}
            if(remaining>0)throw new ArgumentException("Credit exceeds unpaid bill/release AP. Refund settled amounts through their payment workflow first.");
        }
        var lines=new List<FinanceLine>();
        if(retained>0)lines.Add(new(Account(s),retained,0,VendorId:bill.VendorId));
        if(!credit)lines.Add(new(s.Policy.Accounts["ap"],0,ap,VendorId:bill.VendorId));
        else
        {
            if(ap>0)lines.Add(new(s.Policy.Accounts["ap"],ap,0,VendorId:bill.VendorId));
            var source=s.Journals.Single(j=>j.Id==bill.JournalId);var costs=source.Lines.Where(l=>l.Debit>0).ToArray();var total=costs.Sum(l=>l.Debit);var exact=costs.Select(l=>c.Amount*l.Debit/total).ToArray();var amounts=exact.Select(v=>decimal.Floor(v*100)/100).ToArray();var cents=(int)((c.Amount-amounts.Sum())*100);
            foreach(var index in Enumerable.Range(0,costs.Length).OrderByDescending(i=>exact[i]-amounts[i]).ThenBy(i=>i).Take(cents))amounts[index]+=0.01m;
            for(var i=0;i<costs.Length;i++)if(amounts[i]>0)lines.Add(costs[i] with{Debit=0,Credit=amounts[i]});
        }
        var journal=FinanceLedger.Post(s,c.Date,credit?"ap-retainage-credit":"ap-retainage-release",c.SourceKey,c.Reason,lines.ToArray(),actor);
        if(!credit)s.OpenItems.Add(new(c.Id,"AP",bill.VendorId,"RET-"+bill.Number,c.Date,c.Date.AddDays(30),ap,journal.Id));
        else
        {
            if(ap>0)s.Settlements.Add(new(c.Id,"vendor-credit",bill.VendorId,c.Date,ap,"","credit",c.Reason,c.SourceKey,journal.Id,allocations.ToArray()));
            foreach(var line in lines.Where(l=>l.Credit>0&&l.JobId is not null))s.Costs.Add(new(Guid.NewGuid(),line.JobId!.Value,line.Category??"OTHER_DIRECT_COST",line.CostCode,-line.Credit,c.Date,"ap-retainage-credit",c.SourceKey,journal.Id));
        }
        s.Operations.ApRetainageEvents.Add(new(c.Id,bill.Id,c.Date,credit?"CREDIT":"RELEASE",credit?c.Amount:0,retained,ap,journal.Id,allocations.ToArray(),c.SourceKey));
        if(credit)FinanceBillExtraAllocations.ApplyCredits(s);
    }
}
