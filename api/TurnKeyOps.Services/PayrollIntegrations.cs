using MedInsights.Lib.Authorization;
using Microsoft.Extensions.Configuration;
using MedInsights.Repositories.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;

public interface IPayrollAvailability
{
    Task RequireAvailableAsync(Guid tenant,IEnumerable<Guid> membershipIds,DateTime startUtc,DateTime endUtc,CancellationToken ct=default);
}
// Scheduling consumes absence only. Compensation and payroll user grants are irrelevant to availability.
public sealed class PayrollAvailability(IPayrollStore store,ITenantMembershipRepository memberships,IConfiguration? deployment=null):IPayrollAvailability
{
    public async Task RequireAvailableAsync(Guid tenant,IEnumerable<Guid> membershipIds,DateTime startUtc,DateTime endUtc,CancellationToken ct=default)
    {
        if(deployment!=null&&!deployment.GetValue<bool>("Payroll:SchedulingIntegration:Enabled"))return;
        var s=await store.ReadAsync(tenant,ct);if(!s.Configuration.Enabled||!s.Configuration.UsesScheduling)return;
        var ids=membershipIds.ToHashSet();var users=(await memberships.GetActiveAssignedByTenantAsync(tenant,ct)).Where(m=>m.TenantId==tenant&&ids.Contains(m.Id)&&UserModulePermissions.IsActive(m)).Select(m=>m.UserId).ToHashSet();
        var zone=TimeZoneInfo.FindSystemTimeZoneById(s.Configuration.Timezone);var start=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startUtc,zone));var end=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(endUtc.AddTicks(-1),zone));
        if(s.Leave.Any(l=>l.Status=="APPROVED"&&users.Contains(l.EmployeeId)&&l.Date>=start&&l.Date<=end))throw new ArgumentException("Assigned employee has approved time off on this work date.");
    }
}
public sealed class PayrollFinanceCommand
{
    public Guid Id { get; set; }=Guid.NewGuid();public string ExpectedVersion{get;set;}="";public Guid BatchId{get;set;}public string Stage{get;set;}="estimate";public string LiabilityAccount{get;set;}="";public DateOnly Date{get;set;}public string Reason{get;set;}="";
}
// Optional Finance-owned subscriber. Payroll never calls it and never writes the GL.
public sealed class PayrollFinanceConsumer(IFinanceStore finance,IPayrollStore payroll,IFinanceAuthority authority,IPayrollAuthority payrollAuthority,IUserContext user)
{
    public async Task<object> ConsumeAsync(PayrollFinanceCommand c,CancellationToken ct=default)
    {
        await authority.RequireAsync(true,ct);if(!await authority.CanControlAsync(ct)||!await payrollAuthority.AllowsAsync("payroll.compensation.read",ct))throw new MedInsights.Lib.ForbiddenAccessException("Finance control and payroll compensation grants required.");
        PayrollEngine.Required(c.Reason);if(c.Id==Guid.Empty)throw new ArgumentException("Command ID required.");var p=await payroll.ReadAsync(user.TenantId,ct);
        if(!p.Configuration.Enabled||!p.Configuration.UsesFinance)throw new ArgumentException("Optional Finance integration is disabled.");
        var batch=p.Batches.SingleOrDefault(b=>b.Id==c.BatchId)??throw new ArgumentException("Payroll batch not found.");
        var s=await finance.ReadAsync(user.TenantId,ct);var prior=JobConfigurationService.Clone(s);var fingerprint=PayrollEngine.Hash(new{c.BatchId,c.Stage,c.LiabilityAccount,c.Date,c.Reason});var replay=s.Audit.SingleOrDefault(a=>a.CommandId==c.Id);if(replay!=null){if(replay.Fingerprint!=fingerprint)throw new ArgumentException("Command identity conflict.");return new{s.Version,Replayed=true};}if(s.Version!=c.ExpectedVersion)throw new InvalidOperationException("Finance changed.");
        Apply(s,p,batch,c,user.UserId.ToString("D"));s.Audit.Add(new(c.Id,"payroll:"+c.Stage,c.BatchId.ToString(),user.UserId.ToString("D"),DateTime.UtcNow,c.Reason,fingerprint));FinanceIntegrity.ValidateTransition(prior,s);await finance.SaveAsync(user.TenantId,s,c.ExpectedVersion,ct);return new{Version=(await finance.ReadAsync(user.TenantId,ct)).Version};
    }
    public static void Apply(FinanceState s,PayrollState p,PayrollBatch batch,PayrollFinanceCommand c,string actor)
    {
        if(batch.Status=="VOID")throw new ArgumentException("Voided payroll batch cannot feed accounting.");
        if(c.Stage is not("estimate" or "actual"))throw new ArgumentException("Choose estimate or actual reconciliation.");
        if(!s.Accounts.Any(a=>a.Code==c.LiabilityAccount&&a.Active&&a.Type=="LIABILITY")||s.Policy.Accounts.Values.Contains(c.LiabilityAccount)||s.Construction.Accounts.Values.Contains(c.LiabilityAccount))throw new ArgumentException("Dedicated payroll liability account required.");
        if(!p.Events.Any(e=>e.SourceId==batch.Id&&e.Type=="PayrollBatchPrepared"))throw new ArgumentException("Authoritative prepared event required.");
        var source=$"payroll-batch:{batch.Id}:{c.Stage}";if(s.Journals.Any(j=>j.SourceType=="payroll-integration"&&j.SourceId==source))return;
        var lines=batch.Lines.Where(l=>l.JobId!=null).ToArray();if(lines.Length==0)throw new ArgumentException("Batch has no Job costs to allocate.");
        if(lines.Any(l=>!s.CostCodes.Any(code=>code.Active&&code.Code==l.CostCode&&code.Category=="LABOR")))throw new ArgumentException("Map operational activity codes to active Finance labor codes before consumption.");
        var values=new Dictionary<(Guid Job,string Code),decimal>();
        if(c.Stage=="estimate")
        {if(lines.Any(l=>l.LaborCostRate==null))throw new ArgumentException("Missing loaded labor-cost snapshot. Pay rates are never substituted.");foreach(var g in lines.GroupBy(l=>(Job:l.JobId!.Value,Code:l.CostCode)))values[g.Key]=Math.Round(g.Sum(l=>l.Hours*l.LaborCostRate!.Value),2);}
        else
        {
            if(batch.Status!="PROCESSED"||PayrollEngine.Reconcile(p,batch).Length>0||!p.Events.Any(e=>e.SourceId==batch.Id&&e.Type=="PayrollBatchProcessed"))throw new ArgumentException("Processed exact provider reconciliation required.");
            var estimateSource=$"payroll-batch:{batch.Id}:estimate";if(!s.Journals.Any(j=>j.SourceType=="payroll-integration"&&j.SourceId==estimateSource))throw new ArgumentException("Consume the estimate before reconciling actual Job cost.");
            foreach(var earnings in batch.Lines.GroupBy(l=>(l.EmployeeId,l.Type)))
            {
                var actual=PayrollEngine.CurrentActuals(p).Single(a=>a.BatchId==batch.Id&&a.EmployeeId==earnings.Key.EmployeeId&&a.EarningType==earnings.Key.Type);
                if(actual.Gross==null||actual.EmployerTaxes==null||actual.Benefits==null||actual.Burden==null)throw new ArgumentException("Actual employer cost requires explicit gross, employer tax, benefits and additional burden amounts, including explicit zero values.");
                var total=actual.Gross.Value+actual.EmployerTaxes.Value+actual.Benefits.Value+actual.Burden.Value;var hours=earnings.Sum(l=>l.Hours);if(hours<=0)throw new ArgumentException("Signed adjustment actual allocation requires reviewed manual Finance reconciliation.");
                var groups=earnings.GroupBy(l=>(l.JobId,l.CostCode)).ToArray();decimal used=0;for(var i=0;i<groups.Length;i++){var amount=i==groups.Length-1?total-used:Math.Round(total*groups[i].Sum(l=>l.Hours)/hours,2);used+=amount;if(groups[i].Key.JobId is Guid job){var key=(job,groups[i].Key.CostCode);values[key]=values.GetValueOrDefault(key)+amount;}}
            }
            foreach(var cost in s.Costs.Where(v=>v.SourceType=="payroll-integration"&&v.SourceId==estimateSource)){var key=(cost.JobId,cost.CostCode??"");values[key]=values.GetValueOrDefault(key)-cost.Amount;}
        }
        var nonzero=values.Where(kv=>kv.Value!=0).ToArray();if(nonzero.Length==0)return;
        var ledgerLines=nonzero.Select(v=>new FinanceLine(s.Policy.Accounts["cost"],Math.Max(0,v.Value),Math.Max(0,-v.Value),v.Key.Job,v.Key.Code,Category:"LABOR")).ToList();var sum=nonzero.Sum(v=>v.Value);if(sum!=0)ledgerLines.Add(new(c.LiabilityAccount,Math.Max(0,-sum),Math.Max(0,sum)));
        var journal=FinanceLedger.Post(s,c.Date,"payroll-integration",source,c.Reason,ledgerLines.ToArray(),actor);
        foreach(var value in nonzero)s.Costs.Add(new(Guid.NewGuid(),value.Key.Job,"LABOR",value.Key.Code,value.Value,c.Date,"payroll-integration",source,journal.Id));
    }
}
