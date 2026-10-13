using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceOperationsTests
{
    private static readonly DateOnly Day=new(2026,9,15);
    private static FinanceState State(){var s=FinanceTests.State();s.Accounts.Add(new("2200","Accrued payroll","LIABILITY","CREDIT"));s.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});return s;}
    [Fact]public void ApprovedTimePostsSnapshotAndRejectsDuplicateApproval()
    {
        var s=State();var employee=Guid.NewGuid();var job=Guid.NewGuid();var id=Guid.NewGuid();
        FinanceOperationsEngine.Apply(s,new(){Action="labor-rate",Rate=new(Guid.NewGuid(),employee,Day,25,5,"")},"owner");
        FinanceOperationsEngine.Apply(s,new(){Action="submit-time",Time=new(id,"time:1",employee,job,Day,8,"LABOR","FORGED",RateSnapshot:999)},"owner");
        Assert.Null(s.Operations.Time[0].RateSnapshot);Assert.Single(FinanceOperationsEngine.CloseIssues(s,s.Periods[0]));
        FinanceOperationsEngine.Apply(s,new(){Action="approve-time",TargetId=id,OffsetAccount="2200"},"owner");
        Assert.Empty(s.Costs);
        FinanceOperationsEngine.Apply(s,new(){Action="labor-rate",Rate=new(Guid.NewGuid(),employee,Day.AddDays(1),99,0,"")},"owner");
        Assert.Equal(30,s.Operations.Time.Single().RateSnapshot);
        FinanceOperationsEngine.Apply(s,new(){Action="post-time",TargetId=id,OffsetAccount="2200"},"owner");
        Assert.Equal(240,s.Costs.Single().Amount);Assert.Equal(30,s.Operations.Time.Single().RateSnapshot);Assert.Empty(FinanceOperationsEngine.CloseIssues(s,s.Periods[0]));
        Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="approve-time",TargetId=id,OffsetAccount="2200"},"owner"));
        Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="submit-time",Time=new(Guid.NewGuid(),"time:1",employee,job,Day,8,"LABOR","")},"owner"));
        FinanceIntegrity.ValidateTransition(new(),s);
    }
    [Fact]public void AccrualReversalPreservesJobCostAndBlocksDueClose()
    {
        var s=State();var job=Guid.NewGuid();var id=Guid.NewGuid();
        FinanceOperationsEngine.Apply(s,new(){Action="post-accrual",Accrual=new(id,Day,"Labor accrued",[new("5000",100,0,job,"LABOR",Category:"LABOR"),new("2200",0,100)],new(2026,10,1),Guid.Empty)},"owner");
        Assert.Single(FinanceOperationsEngine.CloseIssues(s,s.Periods[1]));
        FinanceOperationsEngine.Apply(s,new(){Action="reverse-accrual",TargetId=id},"owner");Assert.Equal(0,s.Costs.Sum(x=>x.Amount));Assert.Equal(s.Journals[0].Id,s.Journals[1].ReversesId);
        Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="reverse-accrual",TargetId=id},"owner"));
        FinanceIntegrity.ValidateTransition(new(),s);
    }
    [Fact]public void BankFeedDeduplicatesAndPayoutMatchesManyCashEvents()
    {
        var s=State();var j1=FinanceLedger.Post(s,Day,"opening","1","Cash",[new("1000",60,0),new("3000",0,60)],"owner");var j2=FinanceLedger.Post(s,Day,"opening","2","Cash",[new("1000",40,0),new("3000",0,40)],"owner");
        var input=new FinanceOperationsCommand{Action="ingest-bank",SourceKey="provider/account",BankTransactions=[new(){CashAccountId="operating",ExternalId="external-1",Date=Day,Amount=100,Reference="payout"}]};
        FinanceOperationsEngine.Apply(s,input,"owner");FinanceOperationsEngine.Apply(s,input,"owner");Assert.Single(s.BankTransactions);
        var bank=s.BankTransactions[0];FinanceOperationsEngine.Apply(s,new(){Action="match-bank-split",TargetId=bank.Id,Reason="Reviewed processor payout",Splits=[new(bank.Id,j1.Id,60,""),new(bank.Id,j2.Id,40,"")]},"owner");Assert.Equal(2,s.Operations.BankSplits.Count);
        Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="match-bank-split",TargetId=bank.Id,Reason="Duplicate",Splits=[new(bank.Id,j1.Id,100,"")]},"owner"));
    }
    [Fact]public void ClosedPeriodsRejectTimeAndRequiredTasksBlockClose()
    {
        var s=State();FinanceOperationsEngine.Apply(s,new(){Action="close-task",CloseTask=new("2026-09","review","Review reconciliation",true,false,"")},"owner");Assert.Single(FinanceOperationsEngine.CloseIssues(s,s.Periods[0]));
        s.Periods[0].Status="CLOSED";Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="submit-time",Time=new(Guid.NewGuid(),"t",Guid.NewGuid(),Guid.NewGuid(),Day,1,"LABOR","")},"owner"));
    }
    [Fact]public void DraftAccrualAndDueReversalRequireSeparateExplicitActions()
    {
        var s=State();var id=Guid.NewGuid();var accrual=new FinanceAccrual(id,Day,"Reviewable draft",[new("6000",50,0),new("2200",0,50)],new(2026,10,1),Guid.Empty);
        FinanceOperationsEngine.Apply(s,new(){Action="draft-accrual",Accrual=accrual},"owner");Assert.Empty(s.Journals);Assert.Contains(FinanceOperationsEngine.CloseIssues(s,s.Periods[0]),x=>x.Contains("draft"));
        FinanceOperationsEngine.Apply(s,new(){Action="post-accrual-draft",TargetId=id},"owner");Assert.Single(s.Journals);Assert.Empty(s.Operations.AccrualDrafts);
        FinanceOperationsEngine.Apply(s,new(){Action="generate-due-reversals",Date=new(2026,10,1)},"owner");Assert.Equal(2,s.Journals.Count);
        FinanceOperationsEngine.Apply(s,new(){Action="generate-due-reversals",Date=new(2026,10,1)},"owner");Assert.Equal(2,s.Journals.Count);
    }
    [Fact]public void FeedConflictingDuplicateAndSplitReuseRejected()
    {
        var s=State();var input=new FinanceOperationsCommand{Action="ingest-bank",SourceKey="provider/account",BankTransactions=[new(){CashAccountId="operating",ExternalId="e",Date=Day,Amount=100,Reference="Deposit"}]};FinanceOperationsEngine.Apply(s,input,"owner");
        input.BankTransactions[0].Amount=101;Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,input,"owner"));
        var journal=FinanceLedger.Post(s,Day,"opening","p","Deposit",[new("1000",100,0),new("3000",0,100)],"owner");var bank=s.BankTransactions.Single();
        Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="match-bank-split",TargetId=bank.Id,Splits=[new(bank.Id,journal.Id,100,"")]},"owner"));
        FinanceOperationsEngine.Apply(s,new(){Action="match-bank-split",TargetId=bank.Id,Reason="Reviewed bank",Splits=[new(bank.Id,journal.Id,100,"")]},"owner");
        var input2=new FinanceOperationsCommand{Action="ingest-bank",SourceKey="provider/account",BankTransactions=[new(){CashAccountId="operating",ExternalId="e2",Date=Day,Amount=100,Reference="Deposit 2"}]};FinanceOperationsEngine.Apply(s,input2,"owner");
        Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="match-bank-split",TargetId=s.BankTransactions[1].Id,Reason="Reviewed bank",Splits=[new(bank.Id,journal.Id,100,"")]},"owner"));
    }

    [Fact]public void RejectedTimeDoesNotBlockCloseOrConsumeDailyHours()
    {
        var s=State();var employee=Guid.NewGuid();var job=Guid.NewGuid();var id=Guid.NewGuid();
        FinanceOperationsEngine.Apply(s,new(){Action="submit-time",Time=new(id,"original",employee,job,Day,24,"LABOR","")},"owner");
        FinanceOperationsEngine.Apply(s,new(){Action="reject-time",TargetId=id,Reason="Incorrect hours; corrected entry required"},"owner");
        Assert.Empty(FinanceOperationsEngine.CloseIssues(s,s.Periods[0]));
        FinanceOperationsEngine.Apply(s,new(){Action="submit-time",Time=new(Guid.NewGuid(),"corrected",employee,job,Day,8,"LABOR","")},"owner");
        Assert.Equal(2,s.Operations.Time.Count);Assert.Contains(s.Operations.Time,t=>t.Id==id&&t.Status=="REJECTED");
        Assert.Single(FinanceOperationsEngine.CloseIssues(s,s.Periods[0]));
    }

}
