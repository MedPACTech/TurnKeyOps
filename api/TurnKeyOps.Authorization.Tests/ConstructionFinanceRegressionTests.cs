using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Authorization.Tests;
public sealed class ConstructionFinanceRegressionTests
{
    [Fact]public void OctoberPostedWipLocksEarlierSeptemberCostsForIncludedJobOnly()
    {
        var s=FinanceTests.State();var job=Guid.NewGuid();s.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});
        var result=new ConstructionWipResult(job,1000,0,0,500,500,0,0,0,0,0,0,500,50,[]);
        s.Construction.WipSnapshots.Add(new(Guid.NewGuid(),"2026-10",[result],"POSTED","owner",DateTime.UtcNow));
        FinanceLine[] lines=[new("5000",100,0,job),new("3000",0,100)];
        Assert.Throws<ArgumentException>(()=>FinanceLedger.Post(s,new(2026,9,15),"job-cost","locked","Backdated cost",lines,"owner"));
        var other=Guid.NewGuid();FinanceLedger.Post(s,new(2026,9,15),"job-cost","other","Other Job",[new("5000",100,0,other),new("3000",0,100)],"owner");Assert.Single(s.Journals);
    }
    [Theory][InlineData("recognition")][InlineData("retainage")][InlineData("sov")][InlineData("policy")]
    public async Task ImportedBaselineFreezesUsedIdentityMappingsAndScheduledBilledMinimum(string mutation)
    {
        var tenant=Guid.NewGuid();var job=Guid.NewGuid();var customer=Guid.NewGuid();var s=FinanceTests.State();var contract=new ConstructionContract(job,customer,1000,0,[new("one","First",800),new("two","Second",200)]);
        s.Budgets.Add(new(job,"base",1000,0,new(){["MATERIAL"]=500},true,""));s.Construction.Contracts.Add(contract);s.Construction.OpeningBalances.Add(new(job,new(2026,9,1),800,80,800,"source",[new("one",700),new("two",100)]));
        var store=new Mock<IFinanceStore>();store.Setup(v=>v.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(s));var authority=new Mock<IFinanceAuthority>();authority.Setup(v=>v.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var user=new Mock<IUserContext>();user.SetupGet(v=>v.TenantId).Returns(tenant);user.SetupGet(v=>v.UserId).Returns(Guid.NewGuid());var jobs=new Mock<IJobService>();jobs.Setup(v=>v.GetAsync(job,It.IsAny<CancellationToken>())).ReturnsAsync(new JobDto{Id=job,CustomerId=customer});
        var service=new ConstructionFinanceService(store.Object,authority.Object,user.Object,Mock.Of<ISupplyStore>(),jobs.Object);
        var revised=mutation switch{"recognition"=>contract with{RecognitionMethod="COMPLETED_CONTRACT"},"retainage"=>contract with{RetainagePercent=5},"sov"=>contract with{Lines=[new("one","First",800,-200),new("two","Second",200,200)]},_=>contract};
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CommandAsync(new(){Action=mutation=="policy"?"policy":"contract",JobId=job,Contract=revised,Accounts=[]}));store.Verify(v=>v.SaveAsync(It.IsAny<Guid>(),It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
    }
}
