using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class ConstructionFinanceEngine
{
    public static decimal Round(decimal amount)=>Math.Round(amount,2,MidpointRounding.AwayFromZero);
    public static ConstructionWipResult Calculate(FinanceState s,SupplyState supply,ConstructionContract contract,DateOnly date)
    {
        var actual=s.Costs.Where(c=>c.JobId==contract.JobId&&c.Date<=date).Sum(c=>c.Amount);
        var committed=FinanceMigrationCommitments.Remaining(s,supply,contract.JobId,date);
        var forecast=s.Construction.Forecasts.Where(f=>f.JobId==contract.JobId&&f.Date<=date).OrderByDescending(f=>f.Date).ThenByDescending(f=>f.AtUtc).FirstOrDefault();
        var applications=s.Construction.Applications.Where(a=>a.JobId==contract.JobId&&a.IsPosted&&a.PeriodEnd<=date).ToArray();
        var opening=s.Construction.OpeningBalances.SingleOrDefault(b=>b.JobId==contract.JobId&&b.Date<=date);
        var billed=applications.Sum(a=>a.CurrentBilling)+(opening?.Billed??0);
        var retainage=(opening?.Retainage??0)+applications.Sum(a=>a.Retainage)-s.Construction.RetainageReleases.Where(r=>r.JobId==contract.JobId&&r.Date<=date).Sum(r=>r.Amount);
        var errors=new List<string>();
        var budget=s.Budgets.SingleOrDefault(b=>b.JobId==contract.JobId);
        if(budget is null||!budget.ActualsComplete||!budget.TaxBasisKnown)errors.Add("Actual cost completeness must be reviewed.");
        if(FinanceBillExtraAllocations.Outstanding(s,contract.JobId,date))errors.Add("Unallocated bill costs require review.");
        if(forecast is not null&&forecast.BasisHash.Length>0&&forecast.BasisHash!=ForecastBasis(s,contract.JobId))errors.Add("Contract or approved cost budget changed; forecast review is required.");
        if(forecast is null||!forecast.Complete)errors.Add("A complete authorized EAC forecast is required.");
        decimal? final=forecast is null||!forecast.Complete?null:actual+Math.Max(committed,forecast.RemainingForecast);
        if(forecast?.Complete==true&&forecast.Costs!=null){var codeResults=ConstructionCodeBudgets.Report(s,supply,contract.JobId,date);final=codeResults.GroupBy(r=>r.Category).Sum(group=>group.All(r=>r.EstimatedFinalCost.HasValue)?group.Sum(r=>r.EstimatedFinalCost!.Value):group.Sum(r=>r.Actual)+Math.Max(group.Sum(r=>r.RemainingCommitment),forecast.Costs.Where(c=>c.Category==group.Key).Sum(c=>c.RemainingForecast)));}
        if(final is null||final<=0)errors.Add("Estimated final cost must be positive.");
        if(actual<0)errors.Add("Actual cost cannot be negative for cost-to-cost recognition.");
        decimal? percent=errors.Count>0?null:contract.RecognitionMethod=="COMPLETED_CONTRACT"?(contract.Completed?1m:0m):Math.Min(1m,actual/final!.Value);
        decimal? earned=percent is null?null:Round(contract.CurrentValue*percent.Value);
        return new(contract.JobId,contract.CurrentValue,actual,committed,forecast?.RemainingForecast,final,percent,earned,billed,retainage,earned is null?null:Math.Max(0,earned.Value-billed),earned is null?null:Math.Max(0,billed-earned.Value),errors.Count>0||final is null?null:contract.CurrentValue-final,errors.Count>0||final is null||contract.CurrentValue==0?null:(contract.CurrentValue-final)/contract.CurrentValue*100,errors.ToArray(),s.Settlements.Where(p=>p.Date<=date&&p.Kind is "payment" or "refund").Sum(p=>p.Allocations.Where(a=>s.OpenItems.Any(i=>i.Id==a.OpenItemId&&i.JobId==contract.JobId)).Sum(a=>p.Kind=="refund"?-a.Amount:a.Amount)),s.Journals.Where(j=>j.Date<=date).SelectMany(j=>j.Lines).Where(l=>l.JobId==contract.JobId&&l.Account==s.Policy.Accounts["revenue"]).Sum(l=>l.Credit-l.Debit)+(opening?.EarnedRevenue??0),earned is null?null:Math.Max(0,contract.CurrentValue-earned.Value));
    }
    public static void ValidateContract(FinanceState s,ConstructionContract c)
    {
        var budget=s.Budgets.SingleOrDefault(b=>b.JobId==c.JobId)??throw new ArgumentException("Sync and review the canonical accepted Job budget first.");
        if(c.CustomerId==Guid.Empty||c.OriginalValue!=budget.Sold||c.ApprovedChanges!=budget.ApprovedChanges)throw new ArgumentException("Contract must match canonical original contract and approved changes.");
        if(c.CurrentValue<=0||c.RetainagePercent<0||c.RetainagePercent>100||c.RecognitionMethod is not ("COST_TO_COST" or "COMPLETED_CONTRACT"))throw new ArgumentException("Invalid contract policy.");
        if(c.Lines.Length==0||c.Lines.Length>200||c.Lines.Select(l=>l.Id).Distinct().Count()!=c.Lines.Length||c.Lines.Sum(l=>l.OriginalValue)!=c.OriginalValue||c.Lines.Sum(l=>l.ApprovedChanges)!=c.ApprovedChanges)throw new ArgumentException("Unique SOV lines must reconcile to original value and approved changes.");
        foreach(var l in c.Lines){FinanceLedger.Required(l.Id);FinanceLedger.Required(l.Description);FinanceLedger.Money(l.OriginalValue);FinanceLedger.Money(l.ApprovedChanges);if(l.CostCode!=null&&!s.CostCodes.Any(code=>code.Code==l.CostCode&&code.Active))throw new ArgumentException("SOV cost code must be active.");if(l.CurrentValue<0)throw new ArgumentException("Scheduled values cannot be negative.");}
    }
    public static ConstructionPayApplication ValidateApplication(FinanceState s,ConstructionPayApplication a)
    {
        var contract=s.Construction.Contracts.Single(c=>c.JobId==a.JobId);
        if(!contract.ProgressBillingEnabled||a.Id==Guid.Empty||a.PeriodStart==default||a.PeriodEnd<a.PeriodStart||a.Lines.Length==0||a.Lines.Select(l=>l.SovLineId).Distinct().Count()!=a.Lines.Length)throw new ArgumentException("Invalid progress billing application.");
        FinanceLedger.Required(a.Number);
        var prior=s.Construction.Applications.Where(p=>p.JobId==a.JobId&&p.Id!=a.Id&&p.IsPosted).ToArray();
        if(prior.Any(p=>p.PeriodEnd>a.PeriodStart))throw new ArgumentException("Application cannot overlap or predate prior posted billing.");
        var baseline=s.Construction.OpeningBalances.SingleOrDefault(b=>b.JobId==a.JobId);
        if(baseline!=null&&a.PeriodStart<=baseline.Date)throw new ArgumentException("Billing must follow construction cutover.");
        foreach(var line in a.Lines)
        {
            FinanceLedger.Money(line.CurrentBilling);FinanceLedger.Money(line.NewStoredMaterials);FinanceLedger.Money(line.InstalledFromStored);
            var sov=contract.Lines.SingleOrDefault(l=>l.Id==line.SovLineId)??throw new ArgumentException("SOV line unavailable.");
            var priorLines=prior.SelectMany(p=>p.Lines).Where(l=>l.SovLineId==line.SovLineId).Concat(baseline?.Lines.Where(l=>l.SovLineId==line.SovLineId)??[]).ToArray();
            var storedAvailable=priorLines.Sum(l=>l.NewStoredMaterials-l.InstalledFromStored);
            if(line.CurrentBilling<0||line.NewStoredMaterials<0||line.InstalledFromStored<0||line.InstalledFromStored>storedAvailable)throw new ArgumentException("Installation cannot exceed previously billed stored materials; work and stored amounts cannot be negative.");
            if(line.BillableAmount+priorLines.Sum(l=>l.BillableAmount)>sov.CurrentValue)throw new ArgumentException("Billing exceeds approved scheduled value.");
            if(line.NewStoredMaterials>0&&(a.Documents is null||a.Documents.Length==0||a.Documents.Any(string.IsNullOrWhiteSpace)))throw new ArgumentException("New stored materials require supporting documentation references.");
        }
        var gross=a.Lines.Sum(l=>l.BillableAmount);FinanceLedger.Money(gross);
        if(gross<=0&&a.Lines.Sum(l=>l.InstalledFromStored)<=0)throw new ArgumentException("Application must bill new work/materials or install previously billed stored materials.");
        var held=Round(gross*contract.RetainagePercent/100);
        var withheld=AllocateRetainage(a.Lines,held);
        return a with{Lines=a.Lines.Select((line,index)=>line with{RetainageWithheld=withheld[index]}).ToArray(),PriorBilled=(baseline?.Billed??0)+prior.Sum(p=>p.CurrentBilling),CurrentBilling=gross,Retainage=held,TotalDue=gross-held};
    }
    public static decimal[] AllocateRetainage(ConstructionApplicationLine[] lines,decimal total)
    {
        var gross=lines.Sum(l=>l.BillableAmount);if(gross==0)return new decimal[lines.Length];
        var exact=lines.Select(l=>total*l.BillableAmount/gross).ToArray();var amounts=exact.Select(a=>decimal.Floor(a*100)/100).ToArray();var cents=(int)((total-amounts.Sum())*100);
        foreach(var index in Enumerable.Range(0,lines.Length).OrderByDescending(i=>exact[i]-amounts[i]).ThenBy(i=>lines[i].SovLineId,StringComparer.Ordinal).Take(cents))amounts[index]+=0.01m;
        return amounts;
    }
    public static decimal SovRetainage(FinanceState state,Guid job,string line,DateOnly date)
    {
        decimal total=0;foreach(var app in state.Construction.Applications.Where(a=>a.JobId==job&&a.IsPosted&&a.PeriodEnd<=date)){var allocations=AllocateRetainage(app.Lines,app.Retainage);total+=app.Lines.Select((l,i)=>new{l.SovLineId,Amount=l.RetainageWithheld??allocations[i]}).Where(l=>l.SovLineId==line).Sum(l=>l.Amount);}
        var opening=state.Construction.OpeningBalances.SingleOrDefault(b=>b.JobId==job&&b.Date<=date);if(opening!=null){var allocations=AllocateRetainage(opening.Lines,opening.Retainage);total+=opening.Lines.Select((l,i)=>new{l.SovLineId,Amount=l.RetainageWithheld??allocations[i]}).Where(l=>l.SovLineId==line).Sum(l=>l.Amount);}return total;
    }
    public static string CommitmentCategory(FinanceState s,SupplyState supply,Guid job,FinanceCommitmentSource source,DateOnly? date=null)
    {
        var coding=s.Construction.CommitmentCodings.Where(c=>c.JobId==job&&c.OrderId==source.OrderId&&c.OrderLineId==source.OrderLineId&&c.Date<=(date??DateOnly.MaxValue)).OrderByDescending(c=>c.Date).ThenByDescending(c=>c.AtUtc).FirstOrDefault();
        if(coding!=null)return s.CostCodes.Single(c=>c.Code==coding.CostCode).Category;
        var mapped=s.ImportedCommitments.SingleOrDefault(c=>c.JobId==job&&c.SupplyOrderId==source.OrderId&&c.SupplyOrderLineId==source.OrderLineId);
        if(mapped!=null)return FinanceLedger.Categories.Contains(mapped.Category)?mapped.Category:"UNCLASSIFIED";
        var line=supply.Orders.Single(o=>o.Id==source.OrderId).Lines.Single(l=>l.Id==source.OrderLineId);
        var itemId=line.ItemId??supply.Demands.SingleOrDefault(d=>d.Id==line.DemandId)?.ItemId;
        var kind=supply.Catalog.SingleOrDefault(c=>c.Id==itemId)?.Kind;
        return kind!=null&&FinanceLedger.Categories.Contains(kind)?kind:"UNCLASSIFIED";
    }
    public static string ForecastBasis(FinanceState s,Guid job)=>FinanceLedger.Hash(new{Contract=s.Construction.Contracts.SingleOrDefault(c=>c.JobId==job),Budget=s.Budgets.Where(b=>b.JobId==job).Select(b=>new{b.Sold,b.ApprovedChanges,b.Categories}).ToArray()});
    public static string[] CloseIssues(FinanceState s,FinancePeriod period)
    {
        var errors=new List<string>();
        if(s.Construction.Contracts.Count==0)return [];
        if(!s.Construction.WipSnapshots.Any(w=>w.PeriodId==period.Id&&w.Status=="POSTED"))errors.Add("Construction WIP snapshot must be approved and posted for this period.");
        if(s.Construction.Applications.Any(a=>a.PeriodEnd<=period.End&&!a.IsPosted&&a.Status!="VOID"))errors.Add("Uninvoiced construction pay applications require review.");
        if(s.Construction.Accounts.TryGetValue("retainage",out var account))
        {
            var held=s.Construction.OpeningBalances.Where(b=>b.Date<=period.End).Sum(b=>b.Retainage)+s.Construction.Applications.Where(a=>a.IsPosted&&a.PeriodEnd<=period.End).Sum(a=>a.Retainage)-s.Construction.RetainageReleases.Where(r=>r.Date<=period.End).Sum(r=>r.Amount);
            if(FinanceLedger.AccountBalance(s,account,period.End)!=held)errors.Add("Retainage control does not reconcile to construction subledger.");
        }
        var snapshot=s.Construction.WipSnapshots.SingleOrDefault(w=>w.PeriodId==period.Id&&w.Status=="POSTED");
        if(snapshot!=null)
        {
            if(s.Construction.Accounts.TryGetValue("contractAsset",out var asset)&&FinanceLedger.AccountBalance(s,asset,period.End)!=snapshot.Jobs.Sum(j=>j.Underbilling??0))errors.Add("Contract asset control does not reconcile to WIP.");
            if(s.Construction.Accounts.TryGetValue("contractLiability",out var liability)&&-FinanceLedger.AccountBalance(s,liability,period.End)!=snapshot.Jobs.Sum(j=>j.Overbilling??0))errors.Add("Contract liability control does not reconcile to WIP.");
            if(s.Construction.Accounts.TryGetValue("billing",out var billing)&&FinanceLedger.AccountBalance(s,billing,period.End)!=0)errors.Add("Unrecognized construction billing requires WIP adjustment.");
        }
        return errors.ToArray();
    }
    public static string SourcePayload(FinanceState s,SupplyState supply,DateOnly date)=>System.Text.Json.JsonSerializer.Serialize(new{date,s.Construction.Contracts,CodeBudgets=s.Construction.CodeBudgets.Where(b=>b.Date<=date&&b.Status=="REVIEWED").ToArray(),CommitmentCodings=s.Construction.CommitmentCodings.Where(c=>c.Date<=date).ToArray(),Forecasts=s.Construction.Forecasts.Where(f=>f.Date<=date).ToArray(),Costs=s.Costs.Where(c=>c.Date<=date).Select(c=>new{c.Id,c.JobId,c.Date,c.Category,c.CostCode,c.Amount,c.SourceType,c.SourceId,c.JournalId}).ToArray(),Applications=s.Construction.Applications.Where(a=>a.IsPosted&&a.PeriodEnd<=date).ToArray(),Opening=s.Construction.OpeningBalances,Releases=s.Construction.RetainageReleases.Where(r=>r.Date<=date).ToArray(),Mappings=s.Construction.Accounts,Budgets=s.Budgets,ImportedCommitments=s.ImportedCommitments.Where(c=>c.Date<=date).ToArray(),CommitmentRelief=s.Bills.Where(b=>b.Date<=date&&b.JournalId!=null).Select(b=>new{b.Id,b.OrderId,b.Date,b.Lines}).ToArray(),Commitments=s.Construction.Contracts.Select(c=>new{c.JobId,Sources=FinanceInsights.Commitments(s,supply,c.JobId,date).Select(source=>new{Category=CommitmentCategory(s,supply,c.JobId,source,date),Source=source}).ToArray()}).ToArray()},JobConfigurationService.Json);
    public static void ValidateTransition(ConstructionFinanceState before,ConstructionFinanceState after)
    {
        foreach(var budget in before.CodeBudgets.Where(b=>b.Status=="REVIEWED"))if(!after.CodeBudgets.Any(n=>n.Id==budget.Id&&FinanceLedger.Hash(n)==FinanceLedger.Hash(budget)))throw new ArgumentException("Reviewed cost-code budgets are immutable.");
        foreach(var coding in before.CommitmentCodings)if(!after.CommitmentCodings.Contains(coding))throw new ArgumentException("Commitment coding history is immutable.");
        foreach(var revision in before.ContractRevisions)if(!after.ContractRevisions.Any(n=>n.Id==revision.Id&&FinanceLedger.Hash(n)==FinanceLedger.Hash(revision)))throw new ArgumentException("Contract revision history is immutable.");
        foreach(var approval in before.CustomerApprovals)if(!after.CustomerApprovals.Contains(approval))throw new ArgumentException("Customer approvals are immutable.");
        foreach(var b in before.OpeningBalances)if(!after.OpeningBalances.Any(n=>n.JobId==b.JobId&&FinanceLedger.Hash(b)==FinanceLedger.Hash(n)))throw new ArgumentException("Construction migration history is immutable.");
        foreach(var a in before.Applications.Where(a=>a.Status is "IN_REVIEW" or "APPROVED"))if(!after.Applications.Any(n=>n.Id==a.Id&&FinanceLedger.Hash(a with{Status="",JournalId=null,InvoiceId=null})==FinanceLedger.Hash(n with{Status="",JournalId=null,InvoiceId=null})))throw new ArgumentException("Submitted application content is immutable.");
        foreach(var a in before.Applications.Where(a=>a.IsPosted))if(!after.Applications.Any(n=>n.Id==a.Id&&FinanceLedger.Hash(a)==FinanceLedger.Hash(n)))throw new ArgumentException("Posted pay applications are immutable.");
        foreach(var w in before.WipSnapshots.Where(w=>w.Status=="POSTED"))if(!after.WipSnapshots.Any(n=>n.Id==w.Id&&FinanceLedger.Hash(w)==FinanceLedger.Hash(n)))throw new ArgumentException("Posted WIP snapshots are immutable.");
        if(after.Applications.Select(a=>a.Id).Distinct().Count()!=after.Applications.Count||after.WipSnapshots.Select(w=>w.PeriodId).Distinct().Count()!=after.WipSnapshots.Count||after.RetainageReleases.Select(r=>r.Id).Distinct().Count()!=after.RetainageReleases.Count)throw new ArgumentException("Duplicate construction financial identity.");
        foreach(var group in after.RetainageReleases.GroupBy(r=>r.JobId))if(group.Sum(r=>r.Amount)>(after.OpeningBalances.SingleOrDefault(b=>b.JobId==group.Key)?.Retainage??0)+after.Applications.Where(a=>a.JobId==group.Key&&a.IsPosted).Sum(a=>a.Retainage))throw new ArgumentException("Retainage releases exceed held balance.");
        foreach(var f in before.Forecasts)if(!after.Forecasts.Any(n=>n.Id==f.Id&&FinanceLedger.Hash(n)==FinanceLedger.Hash(f)))throw new ArgumentException("Forecast history is immutable.");
        foreach(var r in before.RetainageReleases)if(!after.RetainageReleases.Contains(r))throw new ArgumentException("Retainage releases are immutable.");
    }
}
