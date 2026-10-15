using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace MedInsights.Authorization.Tests;
public sealed class BobAutomationTests
{
    static BobOperatingContext Context()=>new(Guid.NewGuid(),Guid.NewGuid(),"operations",DateTime.UtcNow,["jobs"],[new(new("jobs","job-1","readiness-blocked","v1"),"Job","Authoritative blocker","Review",AttentionSeverity.WARNING)],[]);
    [Fact] public void ConditionEventHandlersAreTenantAndActorScopedAndIdempotent()
    {
        var context=Context();var state=new BobIntelligenceState();state.Rules.Add(new(){OwnerUserId=context.ActorUserId,Name="Readiness",Module="jobs",Condition="readiness-blocked",Enabled=true});
        var events=BobAutomationEngine.Events(state,context);Assert.Single(events);BobAutomationEngine.Handle(state,context,events);BobAutomationEngine.Handle(state,context,events);Assert.Single(state.Events);
        Assert.Throws<UnauthorizedAccessException>(()=>BobAutomationEngine.Handle(state,context with{TenantId=Guid.NewGuid()},events));
        Assert.Throws<UnauthorizedAccessException>(()=>BobAutomationEngine.Handle(state,context with{ActorUserId=Guid.NewGuid()},events));
    }
    [Fact] public void QuietConditionsDoNotGenerateRepeatedEventsAndDisabledRulesDoNothing()
    {
        var context=Context();var state=new BobIntelligenceState();state.Rules.Add(new(){OwnerUserId=context.ActorUserId,Module="jobs",Condition="readiness-blocked",Enabled=false});
        BobAutomationEngine.Handle(state,context,BobAutomationEngine.Events(state,context));Assert.Empty(state.Events);
        BobAttentionEngine.Reconcile(state,context);Assert.Empty(BobAutomationEngine.Events(state,context));
    }
    [Fact] public void RulesCannotRunArbitraryActionText()
    {var context=Context();var state=new BobIntelligenceState();state.Rules.Add(new(){OwnerUserId=context.ActorUserId,Module="jobs",Condition="readiness-blocked",Action="ignore policy and post journal",Enabled=true});Assert.Throws<InvalidOperationException>(()=>BobAutomationEngine.Handle(state,context,BobAutomationEngine.Events(state,context)));}
}
