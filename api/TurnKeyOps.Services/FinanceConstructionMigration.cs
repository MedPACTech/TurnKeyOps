using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
// Normalized Acumatica/export schedules travel through the existing reviewed opening batch,
// exact fingerprint, source evidence payload, canonical entity maps and single Finance CAS commit.
public static class FinanceConstructionMigration
{
    public static FinanceMigrationCheck[] Validate(FinanceState state,FinanceMigrationRequest request,FinanceLine[] opening)
    {
        if(request.ConstructionContracts is null||request.ConstructionHistory is null||request.ConstructionBudgets is null||request.ConstructionForecasts is null||request.ConstructionContracts.Any(c=>c is null||c.Lines is null||c.Lines.Any(l=>l is null))||request.ConstructionHistory.Any(h=>h is null||h.Lines is null||h.Lines.Any(l=>l is null))||request.ConstructionBudgets.Any(b=>b is null||b.Categories is null)||request.ConstructionForecasts.Any(f=>f is null))throw new ArgumentException("Construction migration sections and rows cannot be null.");
        if(request.ConstructionContracts.Length==0&&request.ConstructionHistory.Length==0){if(request.ConstructionBudgets.Length>0||request.ConstructionForecasts.Length>0)throw new ArgumentException("Construction budgets/forecasts require matching contracts.");return [];}
        if(state.Construction.Contracts.Count>0||state.Construction.OpeningBalances.Count>0)throw new ArgumentException("Construction opening schedules require an empty construction subledger.");
        if(request.ConstructionContracts.Select(x=>x.JobId).Distinct().Count()!=request.ConstructionContracts.Length||request.ConstructionHistory.Select(x=>x.JobId).Distinct().Count()!=request.ConstructionHistory.Length)throw new ArgumentException("Duplicate construction opening job.");
        var checks=new List<FinanceMigrationCheck>();
        void Check(string name,decimal imported,decimal? source){if(source is null)throw new ArgumentException(name+" source control is required.");FinanceLedger.Money(source.Value);checks.Add(new(name,imported,source.Value,imported-source.Value));}
        foreach(var contract in request.ConstructionContracts)
        {
            if(!request.Entities.Any(m=>m.Kind=="job"&&m.Id==contract.JobId)||!request.Entities.Any(m=>m.Kind=="customer"&&m.Id==contract.CustomerId))throw new ArgumentException("Construction contracts require explicit canonical job/customer mappings.");
            FinanceLedger.Positive(contract.OriginalValue);FinanceLedger.Money(contract.ApprovedChanges);
            if(contract.CurrentValue<=0||contract.RetainagePercent<0||contract.RetainagePercent>100||contract.Lines.Length==0||contract.Lines.Select(l=>l.Id).Distinct().Count()!=contract.Lines.Length||contract.Lines.Sum(l=>l.OriginalValue)!=contract.OriginalValue||contract.Lines.Sum(l=>l.ApprovedChanges)!=contract.ApprovedChanges||contract.RecognitionMethod is not("COST_TO_COST" or "COMPLETED_CONTRACT"))throw new ArgumentException("Imported contract/SOV totals or recognition policy are invalid.");
            foreach(var line in contract.Lines){FinanceLedger.Required(line.Id);FinanceLedger.Money(line.OriginalValue);FinanceLedger.Money(line.ApprovedChanges);if(line.CurrentValue<0)throw new ArgumentException("Negative SOV line.");}
        }
        foreach(var history in request.ConstructionHistory)
        {
            var contract=request.ConstructionContracts.SingleOrDefault(c=>c.JobId==history.JobId)??throw new ArgumentException("History requires a matching imported contract.");
            FinanceLedger.Required(history.SourceId);FinanceLedger.Money(history.Billed);FinanceLedger.Money(history.Retainage);FinanceLedger.Money(history.EarnedRevenue);
            if(history.Date!=request.CutoverDate||history.Billed<0||history.Billed>contract.CurrentValue||history.Retainage<0||history.Retainage>history.Billed||history.EarnedRevenue<0||history.EarnedRevenue>contract.CurrentValue||history.Lines.Sum(l=>l.BillableAmount)!=history.Billed||history.Lines.Select(l=>l.SovLineId).Distinct().Count()!=history.Lines.Length)throw new ArgumentException("Construction history source/date/totals are invalid.");
            foreach(var line in history.Lines){if(line.NewStoredMaterials!=0||line.InstalledFromStored!=0)throw new ArgumentException("Imported historical stored materials require a separate verified stored-material opening schedule; unsupported in this adapter.");FinanceLedger.Money(line.BillableAmount);if(line.BillableAmount<0||!contract.Lines.Any(l=>l.Id==line.SovLineId&&l.CurrentValue>=line.BillableAmount))throw new ArgumentException("Historical SOV billing exceeds approved line value.");}
        }
        if(request.ConstructionHistory.Length!=request.ConstructionContracts.Length)throw new ArgumentException("Each construction contract requires explicit historical balances, including zeros.");
        if(request.ConstructionBudgets.Select(b=>b.JobId).Distinct().Count()!=request.ConstructionBudgets.Length||request.ConstructionBudgets.Length!=request.ConstructionContracts.Length)throw new ArgumentException("Each imported contract requires one explicit job budget/completeness schedule.");
        foreach(var budget in request.ConstructionBudgets)
        {
            var contract=request.ConstructionContracts.SingleOrDefault(c=>c.JobId==budget.JobId)??throw new ArgumentException("Budget requires an imported contract.");
            if(state.Budgets.Any(b=>b.JobId==budget.JobId)||budget.Sold!=contract.OriginalValue||budget.ApprovedChanges!=contract.ApprovedChanges||budget.Categories.Count==0||budget.Categories.Any(c=>!FinanceLedger.Categories.Contains(c.Key)||c.Value is null||c.Value<0))throw new ArgumentException("Imported budget must match approved contract and contain explicit nonnegative cost categories.");
            FinanceLedger.Required(budget.SourceId);foreach(var amount in budget.Categories.Values)FinanceLedger.Money(amount!.Value);
        }
        foreach(var forecast in request.ConstructionForecasts)
        {
            if(forecast.Id==Guid.Empty||forecast.Date>request.CutoverDate||forecast.Date==default||forecast.RemainingForecast<0||!request.ConstructionContracts.Any(c=>c.JobId==forecast.JobId))throw new ArgumentException("Forecast must reference imported job and source date before cutover.");
            FinanceLedger.Money(forecast.RemainingForecast);FinanceLedger.Required(forecast.Reason);
        }
        if(request.ConstructionForecasts.Select(f=>f.Id).Distinct().Count()!=request.ConstructionForecasts.Length)throw new ArgumentException("Duplicate forecast source identity.");
        Check("Construction budget",request.ConstructionBudgets.Sum(b=>b.Categories.Values.Sum(v=>v!.Value)),request.Controls.ConstructionBudget);
        Check("Construction contract value",request.ConstructionContracts.Sum(c=>c.CurrentValue),request.Controls.ContractValue);
        Check("Construction billed",request.ConstructionHistory.Sum(c=>c.Billed),request.Controls.ConstructionBilled);
        Check("Construction retained",request.ConstructionHistory.Sum(c=>c.Retainage),request.Controls.Retainage);
        Check("Construction earned revenue",request.ConstructionHistory.Sum(c=>c.EarnedRevenue),request.Controls.EarnedRevenue);
        decimal Balance(string role)
        {if(!state.Construction.Accounts.TryGetValue(role,out var account))throw new ArgumentException("Configure construction accounts before migration.");return opening.Where(l=>l.Account==account).Sum(l=>l.Debit-l.Credit);}
        Check("Retainage opening GL",Balance("retainage"),request.Controls.Retainage);
        Check("Contract asset opening GL",Balance("contractAsset"),request.ConstructionHistory.Sum(h=>Math.Max(0,h.EarnedRevenue-h.Billed)));
        Check("Contract liability opening GL",-Balance("contractLiability"),request.ConstructionHistory.Sum(h=>Math.Max(0,h.Billed-h.EarnedRevenue)));
        Check("Unrecognized billing opening GL",Balance("billing"),0);
        return checks.ToArray();
    }
    public static FinanceLine[] DimensionOpening(FinanceState state,FinanceMigrationRequest request,FinanceLine[] opening)
    {
        if(request.ConstructionHistory.Length==0)return opening;
        var roles=new[]{"retainage","contractAsset","contractLiability"};
        var accounts=roles.Select(r=>state.Construction.Accounts[r]).ToHashSet();
        var result=opening.Where(l=>!accounts.Contains(l.Account)).ToList();
        foreach(var h in request.ConstructionHistory)
        {
            var customer=request.ConstructionContracts.Single(c=>c.JobId==h.JobId).CustomerId;
            void Line(string role,decimal amount){if(amount!=0)result.Add(new(state.Construction.Accounts[role],Math.Max(0,amount),Math.Max(0,-amount),h.JobId,CustomerId:customer,Description:"Construction opening "+h.SourceId));}
            Line("retainage",h.Retainage);Line("contractAsset",Math.Max(0,h.EarnedRevenue-h.Billed));Line("contractLiability",-Math.Max(0,h.Billed-h.EarnedRevenue));
        }
        return result.ToArray();
    }
    public static void Apply(FinanceState state,FinanceMigrationRequest request)
    {
        state.Budgets.AddRange(request.ConstructionBudgets);
        state.Construction.Forecasts.AddRange(request.ConstructionForecasts);
        state.Construction.Contracts.AddRange(request.ConstructionContracts);
        state.Construction.OpeningBalances.AddRange(request.ConstructionHistory);
    }
}
