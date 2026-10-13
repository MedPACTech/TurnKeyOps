using System.Text.Json;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Authorization.Tests;
public sealed class ConstructionFinanceSecurityTests
{
    [Fact]public async Task FinanceAuthorityDenialPreventsAnyTenantStorageRead()
    {
        var authority=new Mock<IFinanceAuthority>();authority.Setup(a=>a.RequireAsync(false,It.IsAny<CancellationToken>())).ThrowsAsync(new UnauthorizedAccessException());
        var store=new Mock<IFinanceStore>();var service=new ConstructionFinanceService(store.Object,authority.Object,Mock.Of<IUserContext>(),Mock.Of<ISupplyStore>(),Mock.Of<IJobService>());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.WorkspaceAsync());store.Verify(s=>s.ReadAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]public async Task FinanceWriterCannotPostOrApproveConstructionWithoutControlAuthority()
    {
        var store=new Mock<IFinanceStore>();var authority=new Mock<IFinanceAuthority>();authority.Setup(a=>a.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service=new ConstructionFinanceService(store.Object,authority.Object,Mock.Of<IUserContext>(),Mock.Of<ISupplyStore>(),Mock.Of<IJobService>());
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>service.CommandAsync(new(){Action="post-wip"}));store.Verify(s=>s.ReadAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]public async Task ConstructionPortalIsScopedAndCustomerApprovalIsAuditedWithoutFinancialControls()
    {
        var tenant=Guid.NewGuid();var customer=Guid.NewGuid();var jobId=Guid.NewGuid();var user=Guid.NewGuid();var applicationId=Guid.NewGuid();var partition=RepositoryKeyHelper.ToTenantPartitionKey(tenant);
        var state=new FinanceState{Version="version"};state.Construction.Contracts.Add(new(jobId,customer,1000,0,[new("one","Work",1000)]));state.Construction.Applications.Add(new(applicationId,jobId,"APP-1",new(2026,9,1),new(2026,9,30),[new("one",100)],"APPROVED",0,100,10,90));
        state.Construction.Forecasts.Add(new(Guid.NewGuid(),jobId,new(2026,9,1),500,true,"Secret internal forecast"));
        var store=new Mock<IFinanceStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(state));
        store.Setup(s=>s.SaveAsync(tenant,It.IsAny<FinanceState>(),"version",It.IsAny<CancellationToken>())).Callback<Guid,FinanceState,string,CancellationToken>((_,value,_,_)=>state=value).Returns(Task.CompletedTask);
        var jobs=new Mock<IJobRepository>();jobs.Setup(j=>j.GetAsync(partition,RepositoryKeyHelper.ToRowKey(jobId),It.IsAny<CancellationToken>())).ReturnsAsync(new Job{Id=jobId,CustomerId=customer,PartitionKey=partition});
        var service=new PortalFinanceService(store.Object,Mock.Of<IInvoiceRepository>(),jobs.Object);
        var actor=new PortalActor(tenant,user,[new(){UserId=user,CustomerId=customer,Scope="job",RecordId=jobId}],new(){FinanceEnabled=true});
        var json=JsonSerializer.Serialize(await service.ConstructionAsync(actor),JobConfigurationService.Json);Assert.Contains("APP-1",json);Assert.DoesNotContain("Secret",json);Assert.DoesNotContain("estimatedFinalCost",json);Assert.DoesNotContain("accounts",json);
        var unrelated=actor with{Grants=[new(){UserId=user,CustomerId=Guid.NewGuid(),Scope="job",RecordId=jobId}]};var hidden=JsonSerializer.Serialize(await service.ConstructionAsync(unrelated),JobConfigurationService.Json);Assert.DoesNotContain("APP-1",hidden);
        var approval=new ConstructionPortalApproval(Guid.NewGuid(),"version","Customer Name");await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.ApproveConstructionAsync(unrelated,applicationId,approval));
        await service.ApproveConstructionAsync(actor,applicationId,approval);await service.ApproveConstructionAsync(actor,applicationId,approval);Assert.Single(state.Construction.CustomerApprovals);Assert.Single(state.Audit);Assert.Equal("APPROVED",state.Construction.Applications.Single().Status);Assert.Empty(state.Journals);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.ApproveConstructionAsync(actor,applicationId,approval with{Name="Changed"}));
        store.Verify(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>()),Times.AtLeastOnce);store.Verify(s=>s.ReadAsync(It.Is<Guid>(id=>id!=tenant),It.IsAny<CancellationToken>()),Times.Never);
    }
}
