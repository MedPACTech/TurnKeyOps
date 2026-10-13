using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Authorization.Tests;
public sealed class ConstructionJobChangeTests
{
    [Fact]public async Task OnlySignedApprovedChangesApplyCostImpactsOnceAndPreserveUnknowns()
    {
        var tenant=Guid.NewGuid();var jobId=Guid.NewGuid();var customer=Guid.NewGuid();var estimate=Guid.NewGuid();var changeId=Guid.NewGuid();
        var state=FinanceTests.State();state.Budgets.Add(new(jobId,"base",1000,0,new(){["MATERIAL"]=100,["LABOR"]=50},true,""));
        var job=new JobDto{Id=jobId,CustomerId=customer,Execution=new(){Changes=[new(){Id=changeId,PricingImpact=true,Status="APPROVED",AcceptedEstimateId=estimate,AcceptedRevision=1,AcceptedDocumentHash="signed"},new(){PricingImpact=true,Status="DRAFT"}]}};
        var packet=new QuoteEstimateDto{Id=estimate,CustomerId=customer,Document=new(){SiteId=job.JobSiteId},RevisionNumber=1,DocumentHash="signed",ApprovalSignature=new(){Total=100,SignedAtUtc=new(2026,9,1)},Pricing=new(){Options=[new(){Id="required",Required=true,Total=100,Lines=[new(){Kind="MATERIAL",Cost=20},new(){Kind="LABOR",Cost=null}]}]}};
        var store=new Mock<IFinanceStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(state));store.Setup(s=>s.SaveAsync(tenant,It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Callback<Guid,FinanceState,string,CancellationToken>((_,value,_,_)=>{FinanceIntegrity.ValidateTransition(state,value);state=value;}).Returns(Task.CompletedTask);
        var jobs=new Mock<IJobService>();jobs.Setup(s=>s.GetAsync(jobId,It.IsAny<CancellationToken>())).ReturnsAsync(job);var estimates=new Mock<IQuoteEstimateService>();estimates.Setup(s=>s.GetAsync(estimate,It.IsAny<CancellationToken>())).ReturnsAsync(packet);
        var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(tenant);user.SetupGet(u=>u.UserId).Returns(Guid.NewGuid());
        var service=new FinanceService(store.Object,Mock.Of<IFinanceAuthority>(),user.Object,Mock.Of<IInvoiceService>(),jobs.Object,Mock.Of<ISupplyStore>(),Mock.Of<ICustomerRepository>(),estimates.Object);
        await service.CommandAsync(new(){Action="sync-job-changes",JobId=jobId});await service.CommandAsync(new(){Action="sync-job-changes",JobId=jobId});
        var budget=Assert.Single(state.Budgets);Assert.Equal(120,budget.Categories["MATERIAL"]);Assert.Null(budget.Categories["LABOR"]);Assert.Equal(100,budget.ApprovedChanges);var recorded=Assert.Single(state.JobChanges);Assert.Equal(20,recorded.CategoryCostImpacts!["MATERIAL"]);Assert.NotEmpty(recorded.ApprovalEvidence);Assert.Equal(new DateTime(2026,9,1),recorded.ApprovedAtUtc);
    }
    [Fact]public void ForecastBasisInvalidatesAfterApprovedContractChange()
    {
        var state=FinanceTests.State();var job=Guid.NewGuid();var contract=new ConstructionContract(job,Guid.NewGuid(),1000,0,[new("one","Contract",1000)]);state.Construction.Contracts.Add(contract);state.Budgets.Add(new(job,"source",1000,0,new(){["MATERIAL"]=500},true,""));
        state.Construction.Forecasts.Add(new(Guid.NewGuid(),job,new(2026,9,1),500,true,"reviewed",BasisHash:ConstructionFinanceEngine.ForecastBasis(state,job)));
        Assert.Empty(ConstructionFinanceEngine.Calculate(state,new(),contract,new(2026,9,30)).Exceptions);state.Construction.Contracts[0]=contract with{ApprovedChanges=100};var result=ConstructionFinanceEngine.Calculate(state,new(),state.Construction.Contracts[0],new(2026,9,30));Assert.Contains(result.Exceptions,e=>e.Contains("forecast review"));Assert.Null(result.EarnedRevenue);
    }
    [Fact]public void SnapshotEvidencePreservesInputsWithoutLaborRate()
    {
        var s=FinanceTests.State();s.Costs.Add(new(Guid.NewGuid(),Guid.NewGuid(),"LABOR",null,100,new(2026,9,1),"time","source",Hours:4,Rate:25));
        var payload=ConstructionFinanceEngine.SourcePayload(s,new(),new(2026,9,30));Assert.Contains("source",payload);Assert.DoesNotContain("\"rate\"",payload);Assert.DoesNotContain("hourlyRate",payload);
    }

    [Fact]public void CommitmentsUseCanonicalCatalogKindAndPreserveUnclassifiedSources()
    {
        var s=new FinanceState();var supply=new SupplyState();var job=Guid.NewGuid();var demand=Guid.NewGuid();var order=Guid.NewGuid();var line=Guid.NewGuid();supply.Demands.Add(new(){Id=demand,JobId=job});supply.Catalog.Add(new(){Id="equipment",Kind="EQUIPMENT"});supply.Orders.Add(new(){Id=order,Number="PO-1",Status="APPROVED",Lines=[new(){Id=line,DemandId=demand,ItemId="equipment",Quantity=2,UnitCost=50}]});
        var source=Assert.Single(FinanceInsights.Commitments(s,supply,job,new(2026,9,30)));Assert.Equal("EQUIPMENT",ConstructionFinanceEngine.CommitmentCategory(s,supply,job,source));supply.Orders[0].Lines[0].ItemId=null;Assert.Equal("UNCLASSIFIED",ConstructionFinanceEngine.CommitmentCategory(s,supply,job,source));
    }
}
