using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
namespace TurnKeyOps.Services;
public interface ISupplyJobReadiness
{
    Task ApplyAsync(Guid jobId,JobExecutionDto? execution,CancellationToken ct=default);
    Task<bool> IsManagedAsync(Guid jobId,Guid requirementId,CancellationToken ct=default);
}
public sealed class SupplyJobReadiness(ISupplyStore store,IUserContext user,ICalendarEventRepository calendar):ISupplyJobReadiness
{
    public async Task<bool> IsManagedAsync(Guid jobId,Guid requirementId,CancellationToken ct=default)=>(await store.ReadAsync(user.TenantId,ct)).Demands.Any(d=>d.JobId==jobId&&d.RequirementId==requirementId&&!d.Cancelled);
    public async Task ApplyAsync(Guid jobId,JobExecutionDto? x,CancellationToken ct=default)
    {
        if(x is null)return;var s=await store.ReadAsync(user.TenantId,ct);var events=await calendar.ListAsync(RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId),ct);
        x.SupplyBlockers.Clear();x.SupplyRequirementIds.Clear();x.SupplyRequirementStatuses.Clear();
        foreach(var d in s.Demands.Where(d=>d.JobId==jobId&&!d.Cancelled)){
            x.SupplyRequirementIds.Add(d.RequirementId);x.SupplyRequirementStatuses[d.RequirementId]=SupplyRules.Readiness(s,d,DateTime.UtcNow).Status;var r=x.Requirements.SingleOrDefault(r=>r.Id==d.RequirementId);var reasons=SupplyRules.Readiness(s,d,DateTime.UtcNow).Reasons.ToList();
            if(r is null||r.Status=="Cancelled")reasons.Add("Requirement changed; reconcile supply allocation");
            else{
                var quantity=r.Quantity;var unit=r.Unit;
                if(d.ItemId is not null){var item=s.Catalog.Single(i=>i.Id==d.ItemId);try{quantity=SupplyRules.Convert(item,quantity,unit);unit=item.Unit;}catch(ArgumentException){reasons.Add("Requirement unit changed; reconcile supply mapping");}}
                if(quantity!=d.Quantity||unit!=d.Unit)reasons.Add("Requirement quantity changed; reconcile supply mapping");
            }
            var workStart=events.Where(e=>e.JobId==jobId&&!e.IsDeleted&&e.EventStatus=="scheduled"&&e.JobEventType!="delivery").Select(e=>(DateTime?)e.StartUtc).Min();
            var due=r?.RequiredAtUtc??workStart??d.RequiredAtUtc;
            var openOrders=s.Orders.Where(o=>o.Status is not ("RECEIVED" or "CLOSED" or "CANCELLED")&&o.Lines.Any(l=>l.DemandId==d.Id));
            if(due.HasValue&&openOrders.Any(o=>o.ExpectedAtUtc>due))reasons.Add("Supply arrival is after the Job needs it");
            if(s.Policy.Trades.GetValueOrDefault(d.Trade)?.DeliveryRequired==true&&d.Strategy=="direct"&&SupplyRules.Readiness(s,d,DateTime.UtcNow).Shortage>0){
                if(!events.Any(e=>e.JobId==jobId&&!e.IsDeleted&&e.EventStatus!="cancelled"&&e.JobEventType=="delivery"&&e.Id==d.RequiredEventId))reasons.Add("Schedule a delivery in the shared Calendar");}
            if(d.Gate=="blocker"&&string.IsNullOrWhiteSpace(d.OverrideReason))x.SupplyBlockers.AddRange(reasons.Select(reason=>$"Material: {d.Description} — {reason}"));
        }
    }
}
