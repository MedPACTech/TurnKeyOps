using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceOperationsServiceTests
{
    [Fact]public async Task NonOwnerCannotReadRatesOrMutateTime()
    {
        var store=new Mock<IFinanceStore>();var authority=new Mock<IFinanceAuthority>();var user=new Mock<IUserContext>();
        var service=new FinanceOperationsService(store.Object,authority.Object,user.Object,Mock.Of<ITenantMembershipRepository>(),Mock.Of<IJobService>(),Mock.Of<IActivityEntryRepository>(),Mock.Of<ISupplyStore>());
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>service.ReadAsync());
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>service.CommandAsync(new(){Action="labor-rate"}));
        store.Verify(x=>x.ReadAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]public async Task ForeignTenantEmployeeIsRejectedBeforeSave()
    {
        var tenant=Guid.NewGuid();var employee=Guid.NewGuid();var s=FinanceTests.State();var store=new Mock<IFinanceStore>();store.Setup(x=>x.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(s);
        var authority=new Mock<IFinanceAuthority>();authority.Setup(x=>x.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var user=new Mock<IUserContext>();user.SetupGet(x=>x.TenantId).Returns(tenant);
        var memberships=new Mock<ITenantMembershipRepository>();memberships.Setup(x=>x.GetActiveAssignedByTenantAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync([new(){TenantId=Guid.NewGuid(),UserId=employee}]);
        var service=new FinanceOperationsService(store.Object,authority.Object,user.Object,memberships.Object,Mock.Of<IJobService>(),Mock.Of<IActivityEntryRepository>(),Mock.Of<ISupplyStore>());
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CommandAsync(new(){Action="labor-rate",Reason="Set rate",Rate=new(Guid.NewGuid(),employee,new(2026,9,1),30,5,"")}));
        store.Verify(x=>x.SaveAsync(It.IsAny<Guid>(),It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]public void AccrualReversalCannotEnterClosedPeriod()
    {
        var s=FinanceTests.State();s.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31),Status="CLOSED"});s.Accounts.Add(new("2200","Accrued liability","LIABILITY","CREDIT"));var id=Guid.NewGuid();
        FinanceOperationsEngine.Apply(s,new(){Action="post-accrual",Accrual=new(id,new(2026,9,15),"Accrual",[new("6000",100,0),new("2200",0,100)],new(2026,10,1),Guid.Empty)},"owner");
        Assert.Throws<ArgumentException>(()=>FinanceOperationsEngine.Apply(s,new(){Action="reverse-accrual",TargetId=id},"owner"));Assert.Single(s.Journals);
    }
    [Fact]public async Task ActivityImportUsesPersistedHoursAndRejectsForeignSource()
    {
        var tenant=Guid.NewGuid();var employee=Guid.NewGuid();var job=Guid.NewGuid();var activityId=Guid.NewGuid();var day=new DateOnly(2026,9,15);var s=FinanceTests.State();
        var store=new Mock<IFinanceStore>();store.Setup(x=>x.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(s);
        var authority=new Mock<IFinanceAuthority>();authority.Setup(x=>x.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var user=new Mock<IUserContext>();user.SetupGet(x=>x.TenantId).Returns(tenant);
        var memberships=new Mock<ITenantMembershipRepository>();memberships.Setup(x=>x.GetActiveAssignedByTenantAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync([new(){TenantId=tenant,UserId=employee}]);
        var jobs=new Mock<IJobService>();jobs.Setup(x=>x.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new(){Id=job}]);
        var partition=MedInsights.Lib.Utils.RepositoryKeyHelper.BuildPartitionKey(tenant,day.ToDateTime(TimeOnly.MinValue));
        var entry=new ActivityItems{Id=activityId,TenantId=tenant,PartitionKey=partition,UserId=employee,EntryDate=day.ToDateTime(TimeOnly.MinValue),Unit="hours",NumericValue=7.5};
        var activities=new Mock<IActivityEntryRepository>();activities.Setup(x=>x.GetForMonthAsync(partition,It.IsAny<CancellationToken>())).ReturnsAsync([entry]);
        var service=new FinanceOperationsService(store.Object,authority.Object,user.Object,memberships.Object,jobs.Object,activities.Object,Mock.Of<ISupplyStore>());
        var command=new FinanceOperationsCommand{Action="import-activity-time",Reason="Reviewed activity",ActivityEntryId=activityId,Time=new(Guid.NewGuid(),"forged",employee,job,day,99,"LABOR","")};
        await service.CommandAsync(command);Assert.Equal(7.5m,s.Operations.Time.Single().Hours);Assert.Equal("activity:"+activityId,s.Operations.Time.Single().SourceKey);
        entry.TenantId=Guid.NewGuid();command.Id=Guid.NewGuid();await Assert.ThrowsAsync<ArgumentException>(()=>service.CommandAsync(command));
    }
    [Fact]public void CsvFeedPreservesQuotedReferenceAndRejectsUnnormalizedAmounts()
    {
        var batch=FinanceBankFeedCsv.Parse("externalId,date,amount,reference\nx1,2026-09-15,100.25,\"Processor, payout\"","provider/account","operating");
        Assert.Equal("Processor, payout",batch.Transactions.Single().Reference);Assert.Equal(100.25m,batch.Transactions.Single().Amount);
        Assert.Throws<ArgumentException>(()=>FinanceBankFeedCsv.Parse("externalId,date,amount,reference\nx1,2026-09-15,1.001,test","provider/account","operating"));
    }

    [Fact]public async Task VendorPaymentEvidenceExcludesPaymentCardsAndContainsNoTaxId()
    {
        var tenant=Guid.NewGuid();var vendor=Guid.NewGuid();var s=FinanceTests.State();s.Operations.Vendors.Add(new(vendor,"RECEIVED",null,null,true,"SUBCONTRACTOR",[]));
        foreach(var method in new[]{"check","credit-card","processor","third-party-network"})s.Settlements.Add(new(Guid.NewGuid(),"vendor-payment",vendor,new(2026,9,15),100,"operating",method,method,method,Guid.NewGuid(),[]));
        var store=new Mock<IFinanceStore>();store.Setup(x=>x.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(s);
        var authority=new Mock<IFinanceAuthority>();authority.Setup(x=>x.CanControlAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var user=new Mock<IUserContext>();user.SetupGet(x=>x.TenantId).Returns(tenant);
        var memberships=new Mock<ITenantMembershipRepository>();memberships.Setup(x=>x.GetActiveAssignedByTenantAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<TenantMembership>());
        var service=new FinanceOperationsService(store.Object,authority.Object,user.Object,memberships.Object,Mock.Of<IJobService>(),Mock.Of<IActivityEntryRepository>(),Mock.Of<ISupplyStore>());
        var output=System.Text.Json.JsonSerializer.SerializeToElement(await service.ReadAsync(year:2026),JobConfigurationService.Json);
        Assert.Single(output.GetProperty("vendorPayments1099")[0].GetProperty("payments").EnumerateArray());
        Assert.DoesNotContain("taxId",output.GetRawText(),StringComparison.OrdinalIgnoreCase);
    }

}
