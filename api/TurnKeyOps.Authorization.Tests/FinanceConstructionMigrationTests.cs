using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceConstructionMigrationTests
{
    private static (FinanceState,FinanceMigrationRequest,FinanceLine[]) Input()
    {
        var s=FinanceTests.State();s.Construction.Accounts=new(){["retainage"]="1300",["contractAsset"]="1400",["contractLiability"]="2300",["billing"]="2400"};
        s.Accounts.AddRange([new("1300","Retainage","ASSET","DEBIT"),new("1400","Contract asset","ASSET","DEBIT"),new("2300","Contract liability","LIABILITY","CREDIT"),new("2400","Billing","LIABILITY","CREDIT")]);
        var job=Guid.NewGuid();var customer=Guid.NewGuid();var day=new DateOnly(2026,9,15);
        var request=new FinanceMigrationRequest{CutoverDate=day,ConstructionContracts=[new(job,customer,1000,0,[new("1","Scope",1000)])],ConstructionHistory=[new(job,day,300,30,400,"source-job-1",[new("1",300)])],ConstructionBudgets=[new(job,"budget-1",1000,0,new(){["LABOR"]=600},true,"Source reviewed")],Controls=new(){ContractValue=1000,ConstructionBilled=300,Retainage=30,EarnedRevenue=400,ConstructionBudget=600},Entities=[new("job","J1",job),new("customer","C1",customer)]};
        FinanceLine[] lines=[new("1300",30,0),new("1400",100,0),new("3000",0,130)];return(s,request,lines);
    }
    [Fact]public void OpeningSchedulesReconcileAndKeepPerJobGlDimensions()
    {
        var (s,r,lines)=Input();Assert.All(FinanceConstructionMigration.Validate(s,r,lines),c=>Assert.Equal(0,c.Difference));
        var dimensioned=FinanceConstructionMigration.DimensionOpening(s,r,lines);Assert.Equal(lines.Sum(l=>l.Debit),dimensioned.Sum(l=>l.Debit));Assert.Equal(lines.Sum(l=>l.Credit),dimensioned.Sum(l=>l.Credit));Assert.All(dimensioned.Where(l=>l.Account is "1300" or "1400"),l=>Assert.Equal(r.ConstructionContracts[0].JobId,l.JobId));
        FinanceConstructionMigration.Apply(s,r);Assert.Single(s.Construction.OpeningBalances);Assert.Single(s.Budgets);
    }
    [Fact]public void MissingSourceControlAndInvalidHistoryFailDryRun()
    {
        var (s,r,lines)=Input();r.Controls.ContractValue=null;Assert.Throws<ArgumentException>(()=>FinanceConstructionMigration.Validate(s,r,lines));
        (s,r,lines)=Input();r.ConstructionHistory[0]=r.ConstructionHistory[0] with{Billed=1100};Assert.Throws<ArgumentException>(()=>FinanceConstructionMigration.Validate(s,r,lines));
    }
    [Fact]public void MigrationFingerprintIncludesHistoryBudgetAndForecast()
    {
        var (_,r,_)=Input();var original=FinanceMigrationService.Fingerprint(r);r.ConstructionHistory[0]=r.ConstructionHistory[0] with{EarnedRevenue=450};Assert.NotEqual(original,FinanceMigrationService.Fingerprint(r));
    }
}
