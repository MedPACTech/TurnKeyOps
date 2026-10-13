using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public static class ConstructionCodeBudgets
{
    public static string CategoryBasis(FinanceState s,Guid job)=>FinanceLedger.Hash(s.Budgets.SingleOrDefault(b=>b.JobId==job)?.Categories);
    public static void Validate(FinanceState s,ConstructionCodeBudgetRevision revision)
    {
        var budget=s.Budgets.SingleOrDefault(b=>b.JobId==revision.JobId)??throw new ArgumentException("Canonical Job budget required before allocating cost codes.");
        if(revision.Lines.Length is <1 or >500||revision.Lines.Select(l=>(l.Category,l.CostCode)).Distinct().Count()!=revision.Lines.Length)throw new ArgumentException("Provide unique category/cost-code allocations.");
        foreach(var line in revision.Lines)
        {
            FinanceLedger.Money(line.Amount);
            if(line.Amount<0||!s.CostCodes.Any(c=>c.Code==line.CostCode&&c.Category==line.Category&&c.Active)||!budget.Categories.TryGetValue(line.Category,out var category)||category is null)throw new ArgumentException("Allocate only known category budgets to active matching cost codes.");
        }
        foreach(var category in budget.Categories.Where(c=>c.Value.HasValue))if(revision.Lines.Where(l=>l.Category==category.Key).Sum(l=>l.Amount)!=category.Value)throw new ArgumentException("Cost-code allocations must reconcile exactly to every known category budget.");
    }
    private sealed record Commitment(string Category,string? Code,decimal Amount,string Source);
    private static Commitment[] Commitments(FinanceState s,SupplyState supply,Guid job,DateOnly date)
    {
        var rows=new List<Commitment>();
        foreach(var source in FinanceInsights.Commitments(s,supply,job,date))
        {
            var imported=s.ImportedCommitments.SingleOrDefault(c=>c.JobId==job&&c.SupplyOrderId==source.OrderId&&c.SupplyOrderLineId==source.OrderLineId&&c.Date<=date);
            var coding=s.Construction.CommitmentCodings.Where(c=>c.JobId==job&&c.OrderId==source.OrderId&&c.OrderLineId==source.OrderLineId&&c.Date<=date).OrderByDescending(c=>c.Date).ThenByDescending(c=>c.AtUtc).FirstOrDefault();
            var category=ConstructionFinanceEngine.CommitmentCategory(s,supply,job,source,date);
            rows.Add(new(category,coding?.CostCode,source.Amount,$"{source.OrderId}:{source.OrderLineId}"));
        }
        foreach(var imported in s.ImportedCommitments.Where(c=>c.JobId==job&&c.SupplyOrderId==null&&c.Date<=date))
        {
            var relief=s.Bills.Where(b=>b.JournalId!=null&&b.Date<=date).SelectMany(b=>b.Lines).Where(l=>l.ImportedCommitmentId==imported.Id).Sum(l=>l.Amount);
            var coding=s.Construction.CommitmentCodings.Where(c=>c.JobId==job&&c.ImportedCommitmentId==imported.Id&&c.Date<=date).OrderByDescending(c=>c.Date).ThenByDescending(c=>c.AtUtc).FirstOrDefault();
            rows.Add(new(imported.Category,coding?.CostCode,Math.Max(0,imported.RemainingAmount-relief),imported.Id));
        }
        return rows.ToArray();
    }
    public static ConstructionCodeReport[] Report(FinanceState s,SupplyState supply,Guid job,DateOnly date)
    {
        var revision=s.Construction.CodeBudgets.Where(b=>b.JobId==job&&b.Status=="REVIEWED"&&b.Date<=date).OrderByDescending(b=>b.Date).ThenByDescending(b=>b.AtUtc).FirstOrDefault();
        var allocated=revision!=null&&revision.CategoryBasisHash==CategoryBasis(s,job);
        var budget=s.Budgets.SingleOrDefault(b=>b.JobId==job);
        var actual=s.Costs.Where(c=>c.JobId==job&&c.Date<=date).ToArray();
        var commitments=Commitments(s,supply,job,date);
        var forecast=s.Construction.Forecasts.Where(f=>f.JobId==job&&f.Date<=date).OrderByDescending(f=>f.Date).ThenByDescending(f=>f.AtUtc).FirstOrDefault();
        var forecastComplete=forecast?.Complete==true&&forecast.Costs!=null&&(forecast.BasisHash.Length==0||forecast.BasisHash==ConstructionFinanceEngine.ForecastBasis(s,job));
        var keys=actual.Select(c=>(c.Category,c.CostCode)).Concat(commitments.Select(c=>(c.Category,c.Code))).Concat(revision?.Lines.Select(l=>(l.Category,(string?)l.CostCode))??[]).Concat(forecast?.Costs?.Select(f=>(f.Category,f.CostCode))??[]).Concat((budget?.Categories.Keys.Select(c=>(c,(string?)null))??[])).Distinct().OrderBy(k=>k.Item1).ThenBy(k=>k.Item2);
        return keys.Select(key=>{
            var category=key.Item1;var code=key.Item2;
            var amounts=actual.Where(c=>c.Category==category&&c.CostCode==code).ToArray();var committed=commitments.Where(c=>c.Category==category&&c.Code==code).ToArray();
            var total=amounts.Sum(c=>c.Amount);var remainingCommitment=committed.Sum(c=>c.Amount);
            var categoryUnallocated=forecast?.Costs?.Any(c=>c.Category==category&&c.CostCode==null&&c.RemainingForecast>0)==true;
            decimal? remaining=forecastComplete&&!(code!=null&&categoryUnallocated)?forecast!.Costs!.Where(c=>c.Category==category&&c.CostCode==code).Sum(c=>c.RemainingForecast):null;
            decimal? allocation=allocated&&code!=null&&budget?.Categories.GetValueOrDefault(category)!=null?revision!.Lines.Where(l=>l.Category==category&&l.CostCode==code).Sum(l=>l.Amount):null;
            var commitmentAllocationUnknown=commitments.Any(c=>c.Category==category&&c.Code==null&&c.Amount>0);
            decimal? eac=remaining.HasValue&&!commitmentAllocationUnknown?total+Math.Max(remaining.Value,remainingCommitment):null;
            var sources=amounts.Select(c=>new ConstructionCostSource(c.SourceType,c.SourceId,c.Amount,c.JournalId)).Concat(committed.Select(c=>new ConstructionCostSource("commitment",c.Source,c.Amount))).ToArray();
            return new ConstructionCodeReport(job,category,code,allocation,remainingCommitment,total,remaining,eac,allocation-total,allocation-eac,allocated,code==null||category=="UNCLASSIFIED",sources,revision==null?"No reviewed cost-code allocation.":allocated?"Reviewed allocation matches the canonical category budget.":"Canonical category budget changed after this reviewed allocation; refreshed review required. Historical category-budget snapshots are retained with the allocation.");
        }).ToArray();
    }
}
