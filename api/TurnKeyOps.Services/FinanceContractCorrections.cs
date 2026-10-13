using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

// Contract corrections append history; they do not reverse invoiced revenue, tax or incurred costs.
public static class FinanceContractCorrections
{
    public static void Cancel(FinanceState s,FinanceCommand c,JobDto job,string actor)
    {
        if(c.JobId!=job.Id||!Guid.TryParse(c.Target,out var changeId))throw new ArgumentException("Canonical Job and change are required.");
        FinanceLedger.Required(c.Reason);FinanceLedger.Required(c.SourceKey);FinanceLedger.Required(actor);
        var original=s.JobChanges.SingleOrDefault(v=>v.JobId==job.Id&&v.ChangeId==changeId)??throw new ArgumentException("Recorded accepted change not found.");
        var operational=job.Execution?.Changes.SingleOrDefault(v=>v.Id==changeId)??throw new ArgumentException("Operational change unavailable; do not infer cancellation.");
        if(operational.Status is not ("CANCELLED" or "DECLINED"))throw new ArgumentException("The source Job change must be explicitly cancelled or declined before financial correction.");
        var existing=s.ContractCorrections.SingleOrDefault(v=>v.SourceKey==c.SourceKey);
        if(existing is not null)
        {
            if(existing.JobId!=job.Id||existing.ChangeId!=changeId||existing.Amount!=c.Amount||existing.Date!=c.Date||existing.Reason!=c.Reason)throw new ArgumentException("Correction source key was reused with different content.");
            return;
        }
        if(c.Date==default||!s.Periods.Any(p=>p.Status=="OPEN"&&c.Date>=p.Start&&c.Date<=p.End))throw new ArgumentException("Correction requires an open accounting period.");
        if(s.ContractCorrections.Any(v=>v.ChangeId==changeId))throw new ArgumentException("This accepted change already has a compensating correction.");
        if(FinanceLedger.Money(c.Amount)!=-original.NetValue)throw new ArgumentException("Cancellation must exactly compensate the original signed net value.");
        var budget=s.Budgets.SingleOrDefault(b=>b.JobId==job.Id)??throw new ArgumentException("Accepted Job budget unavailable.");
        s.ContractCorrections.Add(new(Guid.NewGuid(),job.Id,changeId,c.Date,c.Amount,c.Reason,actor,DateTime.UtcNow,c.SourceKey));
        s.Budgets.Remove(budget);
        s.Budgets.Add(budget with{ApprovedChanges=s.JobChanges.Where(v=>v.JobId==job.Id).Sum(v=>v.NetValue)+s.ContractCorrections.Where(v=>v.JobId==job.Id).Sum(v=>v.Amount),ActualsComplete=false,CompletenessNote="Cancelled signed change compensated. Review budget and incurred costs; invoices require their own credit workflow."});
    }
}
