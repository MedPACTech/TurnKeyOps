using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceContractCorrectionTests
{
    private static readonly DateOnly Day=new(2026,9,15);
    private static (FinanceState State,JobDto Job,FinanceCommand Command) Fixture()
    {
        var s=FinanceTests.State();var job=new JobDto{Id=Guid.NewGuid(),Execution=new(){Changes=[new(){Id=Guid.NewGuid(),Status="CANCELLED",PricingImpact=true}]}};
        var change=job.Execution.Changes[0];s.JobChanges.Add(new(job.Id,change.Id,Guid.NewGuid(),3,"signed-hash",250));s.Budgets.Add(new(job.Id,"baseline",1000,250,new(){["LABOR"]=100},true,"Reviewed"));
        return(s,job,new(){Id=Guid.NewGuid(),JobId=job.Id,Target=change.Id.ToString(),Date=Day,Amount=-250,Reason="Customer cancelled signed scope; reviewed source cancellation",SourceKey="cancel-change-1"});
    }
    [Fact]public void CancellationPreservesSignedSourceAndAppendsExactCompensationWithoutLedgerMutation()
    {
        var (s,job,c)=Fixture();var original=s.JobChanges[0];FinanceContractCorrections.Cancel(s,c,job,"owner");
        Assert.Equal(original,Assert.Single(s.JobChanges));Assert.Equal(-250,Assert.Single(s.ContractCorrections).Amount);Assert.Empty(s.Journals);Assert.Equal(0,Assert.Single(s.Budgets).ApprovedChanges);Assert.False(s.Budgets[0].ActualsComplete);
        FinanceContractCorrections.Cancel(s,c,job,"owner");Assert.Single(s.ContractCorrections);
    }
    [Theory][InlineData("APPROVED")][InlineData("IMPLEMENTED")][InlineData("DRAFT")]
    public void CannotCorrectAnUncancelledOperationalChange(string status)
    {
        var (s,job,c)=Fixture();job.Execution!.Changes[0].Status=status;Assert.Throws<ArgumentException>(()=>FinanceContractCorrections.Cancel(s,c,job,"owner"));Assert.Empty(s.ContractCorrections);
    }
    [Fact]public void WrongAmountClosedPeriodChangedRetryAndDuplicateSourceCannotRewriteContract()
    {
        var (s,job,c)=Fixture();c.Amount=-249;Assert.Throws<ArgumentException>(()=>FinanceContractCorrections.Cancel(s,c,job,"owner"));c.Amount=-250;s.Periods[0].Status="CLOSED";Assert.Throws<ArgumentException>(()=>FinanceContractCorrections.Cancel(s,c,job,"owner"));s.Periods[0].Status="OPEN";
        FinanceContractCorrections.Cancel(s,c,job,"owner");c.Reason="Changed";Assert.Throws<ArgumentException>(()=>FinanceContractCorrections.Cancel(s,c,job,"owner"));c.Reason=s.ContractCorrections[0].Reason;c.SourceKey="another";Assert.Throws<ArgumentException>(()=>FinanceContractCorrections.Cancel(s,c,job,"owner"));
    }
    [Fact]public void CompensatingHistoryCannotBeChangedOrRemoved()
    {
        var (s,job,c)=Fixture();FinanceContractCorrections.Cancel(s,c,job,"owner");var after=JobConfigurationService.Clone(s);after.ContractCorrections.Clear();Assert.Throws<ArgumentException>(()=>FinanceIntegrity.ValidateTransition(s,after));
        after=JobConfigurationService.Clone(s);after.ContractCorrections[0]=after.ContractCorrections[0] with{Amount=-200};Assert.Throws<ArgumentException>(()=>FinanceIntegrity.ValidateTransition(s,after));
    }
}
