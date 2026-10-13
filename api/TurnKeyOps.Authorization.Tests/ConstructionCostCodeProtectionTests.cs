using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Authorization.Tests;
public sealed class ConstructionCostCodeProtectionTests
{
    [Theory][InlineData("budget")][InlineData("forecast")][InlineData("commitment")][InlineData("contract")][InlineData("contract-history")]
    public async Task AuthoritativeConstructionReferencesFreezeCostCodeClassificationBeforeActualCostsExist(string source)
    {
        var state=FinanceTests.State();var job=Guid.NewGuid();state.CostCodes.Add(new("MAT-A","Materials A","MATERIAL"));
        switch(source)
        {
            case "budget":state.Construction.CodeBudgets.Add(new(Guid.NewGuid(),job,new(2026,9,1),[new("MATERIAL","MAT-A",100)],"REVIEWED"));break;
            case "forecast":state.Construction.Forecasts.Add(new(Guid.NewGuid(),job,new(2026,9,1),100,true,"Reviewed",Costs:[new("MATERIAL","MAT-A",100)]));break;
            case "commitment":state.Construction.CommitmentCodings.Add(new(Guid.NewGuid(),job,Guid.NewGuid(),Guid.NewGuid(),"MAT-A"));break;
            case "contract-history":state.Construction.ContractRevisions.Add(new(Guid.NewGuid(),new(job,Guid.NewGuid(),100,0,[new("one","Scheduled work",100,CostCode:"MAT-A")]),"owner",DateTime.UtcNow,"Approved",[]));break;
            case "contract":state.Construction.Contracts.Add(new(job,Guid.NewGuid(),100,0,[new("one","Scheduled work",100,CostCode:"MAT-A")]));break;
        }
        var tenant=Guid.NewGuid();var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(tenant);var store=new Mock<IFinanceStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(state);
        var service=new FinanceService(store.Object,Mock.Of<IFinanceAuthority>(),user.Object,Mock.Of<IInvoiceService>(),Mock.Of<IJobService>(),Mock.Of<ISupplyStore>(),Mock.Of<ICustomerRepository>());
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CommandAsync(new(){Action="cost-code",Code=new("MAT-A","Reclassified","LABOR")}));Assert.Equal("MATERIAL",state.CostCodes.Single(c=>c.Code=="MAT-A").Category);store.Verify(s=>s.SaveAsync(It.IsAny<Guid>(),It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
    }
}
