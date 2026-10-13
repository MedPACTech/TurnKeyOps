using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.Authorization.Tests;
public class ConstructionFinanceTests
{
    private static readonly Guid Job=Guid.NewGuid();
    private static ConstructionContract Contract()=>new(Job,Guid.NewGuid(),100000,0,[new("one","Contract",100000)]);
    private static FinanceState State()
    {
        var s=new FinanceState();s.Budgets.Add(new(Job,"estimate",100000,0,new(){["MATERIAL"]=50000},true,"reviewed"));s.Construction.Contracts.Add(Contract());return s;
    }
    [Fact]public void CostToCostSeparatesEarnedRevenueFromBilling()
    {
        var s=State();s.Costs.Add(new(Guid.NewGuid(),Job,"MATERIAL",null,20000,new(2026,9,10),"bill","1"));s.Construction.Forecasts.Add(new(Guid.NewGuid(),Job,new(2026,9,1),30000,true,"Reviewed"));
        var result=ConstructionFinanceEngine.Calculate(s,new(),s.Construction.Contracts[0],new(2026,9,30));Assert.Equal(0.4m,result.PercentComplete);Assert.Equal(40000,result.EarnedRevenue);Assert.Equal(40000,result.Underbilling);Assert.Equal(0,result.Billed);
    }
    [Fact]public void MissingForecastProducesExceptionRatherThanEarnedRevenue()
    {var s=State();var r=ConstructionFinanceEngine.Calculate(s,new(),s.Construction.Contracts[0],new(2026,9,30));Assert.Null(r.EarnedRevenue);Assert.NotEmpty(r.Exceptions);}
    [Fact]public void ScheduleMustMatchCanonicalContractAndChanges()
    {var s=State();Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateContract(s,Contract() with{Lines=[new("one","Bad",99999)]}));}
    [Fact]public void BillingUsesPriorApplicationsAndRetainage()
    {
        var s=State();var prior=new ConstructionPayApplication(Guid.NewGuid(),Job,"1",new(2026,8,1),new(2026,8,31),[new("one",20000)],"INVOICED",0,20000,2000,18000,Guid.NewGuid());s.Construction.Applications.Add(prior);
        var current=ConstructionFinanceEngine.ValidateApplication(s,new(Guid.NewGuid(),Job,"2",new(2026,9,1),new(2026,9,30),[new("one",30000)]));Assert.Equal(20000,current.PriorBilled);Assert.Equal(3000,current.Retainage);Assert.Equal(27000,current.TotalDue);
        Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateApplication(s,current with{Lines=[new("one",90000)]}));
    }
    [Fact]public void PostedSnapshotsAndBillingCannotBeMutated()
    {
        var before=new ConstructionFinanceState();var a=new ConstructionPayApplication(Guid.NewGuid(),Job,"1",new(2026,9,1),new(2026,9,30),[new("one",100)],JournalId:Guid.NewGuid());before.Applications.Add(a);var after=new ConstructionFinanceState();after.Applications.Add(a with{TotalDue=1});Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateTransition(before,after));
        before=new();before.WipSnapshots.Add(new(Guid.NewGuid(),"2026-09",[],"POSTED","actor",DateTime.UtcNow));Assert.Throws<ArgumentException>(()=>ConstructionFinanceEngine.ValidateTransition(before,new()));
    }

    [Fact]public async Task ProgressBillingRetainageAndWipPostBalancedAndReplaySafely()
    {
        var s=FinanceTests.State();var tenant=Guid.NewGuid();var customer=Guid.NewGuid();
        s.Accounts.AddRange([new("1110","Retainage","ASSET","DEBIT"),new("1120","Contract asset","ASSET","DEBIT"),new("2200","Contract liability","LIABILITY","CREDIT"),new("2210","Billing","LIABILITY","CREDIT")]);
        s.Construction.Accounts=new(){{"retainage","1110"},{"contractAsset","1120"},{"contractLiability","2200"},{"billing","2210"}};
        s.Budgets.Add(new(Job,"estimate",100000,0,new(){["MATERIAL"]=50000},true,"reviewed"));s.Construction.Contracts.Add(Contract() with{CustomerId=customer});
        s.Costs.Add(new(Guid.NewGuid(),Job,"MATERIAL",null,20000,new(2026,9,1),"bill","cost"));
        s.Construction.Forecasts.Add(new(Guid.NewGuid(),Job,new(2026,9,1),30000,true,"reviewed"));
        var store=new Moq.Mock<IFinanceStore>();store.Setup(v=>v.ReadAsync(tenant,Moq.It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(s));
        store.Setup(v=>v.SaveAsync(tenant,Moq.It.IsAny<FinanceState>(),Moq.It.IsAny<string>(),Moq.It.IsAny<CancellationToken>())).Callback<Guid,FinanceState,string,CancellationToken>((_,n,version,_)=>{Assert.Equal(s.Version,version);FinanceIntegrity.ValidateTransition(s,n);n.Version=Guid.NewGuid().ToString();s=JobConfigurationService.Clone(n);}).Returns(Task.CompletedTask);
        var authority=new Moq.Mock<IFinanceAuthority>();authority.Setup(v=>v.CanControlAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var user=new Moq.Mock<TurnKeyOps.Lib.Utils.IUserContext>();user.SetupGet(v=>v.TenantId).Returns(tenant);user.SetupGet(v=>v.UserId).Returns(Guid.NewGuid());
        var jobs=new Moq.Mock<TurnKeyOps.Services.Interfaces.IJobService>();jobs.Setup(v=>v.GetAsync(Job,Moq.It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=Job,CustomerId=customer});
        var supply=new Moq.Mock<ISupplyStore>();supply.Setup(v=>v.ReadAsync(tenant,Moq.It.IsAny<CancellationToken>())).ReturnsAsync(new SupplyState());
        var service=new ConstructionFinanceService(store.Object,authority.Object,user.Object,supply.Object,jobs.Object);
        async Task Run(ConstructionFinanceCommand c){c.ExpectedVersion=s.Version;await service.CommandAsync(c);}
        var app=Guid.NewGuid();await Run(new(){Action="application",JobId=Job,Application=new(app,Job,"1",new(2026,9,1),new(2026,9,30),[new("one",30000)])});
        await Run(new(){Action="submit-application",Target=app});await Run(new(){Action="approve-application",Target=app});await Run(new(){Action="invoice-application",Target=app});
        Assert.Equal(27000,s.OpenItems.Single().Total);Assert.Equal(3000,FinanceLedger.AccountBalance(s,"1110",DateOnly.MaxValue));Assert.Equal(0,FinanceLedger.AccountBalance(s,"4000",DateOnly.MaxValue));
        var release=new ConstructionFinanceCommand{Action="release-retainage",JobId=Job,Date=new(2026,9,30),Amount=1000,Reason="Milestone release"};await Run(release);await service.CommandAsync(release);
        Assert.Single(s.Construction.RetainageReleases);Assert.Equal(2000,FinanceLedger.AccountBalance(s,"1110",DateOnly.MaxValue));
        await Assert.ThrowsAsync<ArgumentException>(()=>Run(new(){Action="release-retainage",JobId=Job,Date=new(2026,9,30),Amount=2001,Reason="Excess"}));
        var snapshot=Guid.NewGuid();await Run(new(){Id=snapshot,Action="calculate-wip",PeriodId="2026-09"});await Run(new(){Action="approve-wip",Target=snapshot});await Run(new(){Action="post-wip",Target=snapshot});
        Assert.Equal(-40000,FinanceLedger.AccountBalance(s,"4000",DateOnly.MaxValue));Assert.Equal(10000,FinanceLedger.AccountBalance(s,"1120",DateOnly.MaxValue));Assert.Equal(0,FinanceLedger.AccountBalance(s,"2210",DateOnly.MaxValue));Assert.Equal("POSTED",s.Construction.WipSnapshots.Single().Status);
        var frozen=FinanceLedger.Hash(s.Construction.WipSnapshots.Single());
        s.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});
        s.Costs.Add(new(Guid.NewGuid(),Job,"MATERIAL",null,10000,new(2026,10,1),"bill","later"));s.Construction.Forecasts.Add(new(Guid.NewGuid(),Job,new(2026,10,1),20000,true,"reviewed"));
        var october=Guid.NewGuid();await Run(new(){Action="application",JobId=Job,Application=new(october,Job,"2",new(2026,10,1),new(2026,10,31),[new("one",20000)])});await Run(new(){Action="submit-application",Target=october});await Run(new(){Action="approve-application",Target=october});await Run(new(){Action="invoice-application",Target=october});
        var next=Guid.NewGuid();await Run(new(){Id=next,Action="calculate-wip",PeriodId="2026-10"});await Run(new(){Action="approve-wip",Target=next});await Run(new(){Action="post-wip",Target=next});
        Assert.Equal(-60000,FinanceLedger.AccountBalance(s,"4000",DateOnly.MaxValue));Assert.Equal(10000,FinanceLedger.AccountBalance(s,"1120",DateOnly.MaxValue));Assert.Equal(frozen,FinanceLedger.Hash(s.Construction.WipSnapshots.Single(w=>w.PeriodId=="2026-09")));
        s.Periods.Single(p=>p.Id=="2026-10").Status="CLOSED";
        await Assert.ThrowsAsync<ArgumentException>(()=>Run(new(){Action="release-retainage",JobId=Job,Date=new(2026,10,31),Amount=1,Reason="Closed"}));
        var conflict=release;conflict.Amount=999;await Assert.ThrowsAsync<ArgumentException>(()=>service.CommandAsync(conflict));

    }

    [Fact]public void CompletedContractWaitsForExplicitCompletion()
    {var s=State();s.Construction.Forecasts.Add(new(Guid.NewGuid(),Job,new(2026,9,1),50000,true,"Reviewed"));var contract=Contract() with{RecognitionMethod="COMPLETED_CONTRACT"};Assert.Equal(0,ConstructionFinanceEngine.Calculate(s,new(),contract,new(2026,9,30)).EarnedRevenue);Assert.Equal(100000,ConstructionFinanceEngine.Calculate(s,new(),contract with{Completed=true},new(2026,9,30)).EarnedRevenue);}
    [Fact]public void IncompleteForecastHasNoProjectedMargin()
    {var s=State();s.Construction.Forecasts.Add(new(Guid.NewGuid(),Job,new(2026,9,1),50000,false,"Unknown scope"));var r=ConstructionFinanceEngine.Calculate(s,new(),Contract(),new(2026,9,30));Assert.Null(r.ProjectedMargin);Assert.Null(r.ProjectedProfit);Assert.Null(r.EarnedRevenue);}
}
