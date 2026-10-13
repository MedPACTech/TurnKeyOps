using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceAccrualWorkerTests
{
    [Fact]public async Task DisabledPolicyDoesNotPostAndBlockedClosedPeriodRetriesAuditedNextDay()
    {
        var tenant=Guid.NewGuid();var state=FinanceTests.State();state.Accounts.Add(new("2200","Accrued liabilities","LIABILITY","CREDIT"));state.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31),Status="CLOSED"});var accrualId=Guid.NewGuid();
        FinanceOperationsEngine.Apply(state,new(){Action="post-accrual",Accrual=new(accrualId,new(2026,9,15),"Accrual",[new("6000",100,0),new("2200",0,100)],new(2026,10,1),Guid.Empty)},"owner");
        var store=new Mock<IFinanceStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(state));
        store.Setup(s=>s.SaveAsync(tenant,It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((Guid _,FinanceState next,string expected,CancellationToken ct)=>{Assert.Equal(state.Version,expected);FinanceIntegrity.ValidateTransition(state,next);next.Version=Guid.NewGuid().ToString();state=next;return Task.CompletedTask;});
        var runner=new FinanceAccrualReversalRunner(store.Object);Assert.Equal(0,await runner.RunTenantAsync(tenant,new(2026,10,1)));Assert.Empty(state.Operations.ReversalAttempts);
        FinanceOperationsEngine.Apply(state,new(){Action="reversal-policy",Enabled=true},"owner");
        Assert.Equal(0,await runner.RunTenantAsync(tenant,new(2026,10,1)));Assert.Equal("BLOCKED",state.Operations.ReversalAttempts.Single().Status);Assert.Single(state.Journals);
        Assert.Equal(0,await runner.RunTenantAsync(tenant,new(2026,10,1)));Assert.Single(state.Operations.ReversalAttempts);
        state.Periods[1].Status="OPEN";Assert.Equal(1,await runner.RunTenantAsync(tenant,new(2026,10,2)));Assert.Equal(2,state.Journals.Count);Assert.Equal(2,state.Operations.ReversalAttempts.Count);Assert.Equal(2,state.Audit.Count);
        Assert.Equal(0,await runner.RunTenantAsync(tenant,new(2026,10,3)));Assert.Equal(2,state.Journals.Count);
    }
    [Fact]public async Task CasConflictLeavesReversalPendingForExactIdRetry()
    {
        var tenant=Guid.NewGuid();var state=FinanceTests.State();state.Accounts.Add(new("2200","Accrual","LIABILITY","CREDIT"));var id=Guid.NewGuid();FinanceOperationsEngine.Apply(state,new(){Action="post-accrual",Accrual=new(id,new(2026,9,1),"Accrual",[new("6000",20,0),new("2200",0,20)],new(2026,9,2),Guid.Empty)},"owner");FinanceOperationsEngine.Apply(state,new(){Action="reversal-policy",Enabled=true},"owner");
        var store=new Mock<IFinanceStore>();store.Setup(s=>s.ReadAsync(tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(state));var calls=0;
        store.Setup(s=>s.SaveAsync(tenant,It.IsAny<FinanceState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((Guid _,FinanceState next,string expected,CancellationToken ct)=>{if(++calls==1)throw new InvalidOperationException("Finance changed");state=next;return Task.CompletedTask;});
        var runner=new FinanceAccrualReversalRunner(store.Object);await Assert.ThrowsAsync<InvalidOperationException>(()=>runner.RunTenantAsync(tenant,new(2026,9,2)));Assert.Single(state.Journals);Assert.Empty(state.Operations.ReversalAttempts);
        Assert.Equal(1,await runner.RunTenantAsync(tenant,new(2026,9,2)));Assert.Equal(2,state.Journals.Count);Assert.Single(state.Operations.ReversalAttempts);
    }

}
