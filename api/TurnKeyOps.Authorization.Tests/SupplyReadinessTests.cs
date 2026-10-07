using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
namespace MedInsights.Authorization.Tests;
public sealed class SupplyReadinessTests
{
    [Fact]public async Task ManagedSupplyCannotUseManuallyAvailableStatusToBypassShortage()
    {
        var tenant=Guid.NewGuid();var job=Guid.NewGuid();var r=new JobRequirementDto{Description="Concrete",Quantity=10,Unit="cubic-yard",Status="Available"};var x=new JobExecutionDto{Requirements=[r]};
        var d=new MaterialDemand{JobId=job,RequirementId=r.Id,Description=r.Description,Quantity=10,Unit=r.Unit,Trade="concrete",Strategy="direct"};var state=new SupplyState{Demands=[d]};
        var store=new Mock<ISupplyStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(state);
        var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(tenant);var calendar=new Mock<ICalendarEventRepository>();calendar.Setup(c=>c.ListAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new List<CalendarEvent>());
        var service=new SupplyJobReadiness(store.Object,user.Object,calendar.Object);await service.ApplyAsync(job,x);
        Assert.Contains(JobExecutionRules.Blockers(x,"start"),b=>b.Contains("Missing 10"));Assert.Contains(x.SupplyBlockers,b=>b.Contains("Schedule a delivery"));
        var order=new PurchaseOrder{Lines=[new(){DemandId=d.Id,Quantity=10,Unit=r.Unit,DirectToJob=true}]};state.Orders.Add(order);state.Receipts.Add(new(){OrderId=order.Id,LineId=order.Lines[0].Id,Accepted=10});
        await service.ApplyAsync(job,x);Assert.Empty(JobExecutionRules.Blockers(x,"start"));
        r.Quantity=11;await service.ApplyAsync(job,x);Assert.Contains(x.SupplyBlockers,b=>b.Contains("quantity changed"));
        d.Gate="warning";await service.ApplyAsync(job,x);Assert.Empty(JobExecutionRules.Blockers(x,"start"));
    }
    [Fact]public async Task MovingWorkEarlierDetectsSupplyMismatchFromSharedCalendar()
    {
        var tenant=Guid.NewGuid();var job=Guid.NewGuid();var r=new JobRequirementDto{Quantity=1};var d=new MaterialDemand{JobId=job,RequirementId=r.Id,Quantity=1};var state=new SupplyState{Demands=[d],Orders=[new(){Status="ACKNOWLEDGED",ExpectedAtUtc=DateTime.UtcNow.AddDays(5),Lines=[new(){DemandId=d.Id,Quantity=1}]}]};
        var store=new Mock<ISupplyStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(state);var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(tenant);
        var calendar=new Mock<ICalendarEventRepository>();calendar.Setup(c=>c.ListAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new List<CalendarEvent>{new(){JobId=job,JobEventType="work",StartUtc=DateTime.UtcNow.AddDays(2)}});
        var x=new JobExecutionDto{Requirements=[r]};await new SupplyJobReadiness(store.Object,user.Object,calendar.Object).ApplyAsync(job,x);Assert.Contains(x.SupplyBlockers,b=>b.Contains("after the Job needs it"));
    }
}
