using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;
public sealed class FinanceOperationsService(IFinanceStore store, IFinanceAuthority authority, IUserContext user, MedInsights.Repositories.Interfaces.ITenantMembershipRepository memberships, TurnKeyOps.Services.Interfaces.IJobService jobs, MedInsights.Repositories.Interfaces.IActivityEntryRepository activities, ISupplyStore supply, FinanceHttpBankFeedProvider? bankProvider=null)
{
    public async Task<object> ReadAsync(CancellationToken ct=default,int? year=null)
    {
        await authority.RequireAsync(false,ct);
        // Labor rates and vendor compliance are restricted to explicit Finance control users.
        if(!await authority.CanControlAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("Finance owner permission is required for labor rates and compliance.");
        var taxYear=year??DateTime.UtcNow.Year;if(taxYear is <1900 or >9999)throw new ArgumentException("Valid tax year required.");
        var state=await store.ReadAsync(user.TenantId,ct);
        return new{state.Version,TaxYear=taxYear,Employees=(await memberships.GetActiveAssignedByTenantAsync(user.TenantId,ct)).Where(m=>m.TenantId==user.TenantId&&!m.IsDeleted).Select(m=>new{EmployeeId=m.UserId,MembershipId=m.Id}),PayrollExport=state.Operations.Time.Where(t=>t.Status=="POSTED").Select(t=>new{t.Id,t.EmployeeId,t.JobId,t.Date,t.Hours,t.EarningsClass,t.BaseRateSnapshot,t.BurdenRateSnapshot,t.JournalId}),State=state.Operations,ApRetainage=state.Bills.Where(b=>b.JournalId is not null&&b.RetainageAmount>0).Select(b=>new{b.Id,b.Number,b.VendorId,Gross=b.Total,Net=b.NetPayable,Withheld=b.RetainageAmount,Held=FinanceApRetainage.Held(state,b,DateOnly.MaxValue)}),Close=state.Periods.Select(p=>new{p.Id,Issues=FinanceOperationsEngine.CloseIssues(state,p)}),VendorPayments1099=state.Operations.Vendors.Where(v=>v.Eligible1099).Select(v=>new{v.VendorId,Payments=state.Settlements.Where(p=>p.Date.Year==taxYear&&p.PartyId==v.VendorId&&p.Kind=="vendor-payment"&&p.Method.ToLowerInvariant() is not ("card" or "credit-card" or "debit-card" or "processor" or "payment-card" or "third-party-network")).Select(p=>new{p.Id,p.Date,p.Amount,p.Reference,p.JournalId})})};
    }
    public async Task<object> CommandAsync(FinanceOperationsCommand c,CancellationToken ct=default)
    {
        await authority.RequireAsync(true,ct);
        if(!await authority.CanControlAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("Explicit Finance owner approval is required.");
        if(c.Id==Guid.Empty)throw new ArgumentException("Command identity required.");FinanceLedger.Required(c.Reason);
        var s=await store.ReadAsync(user.TenantId,ct);
        var fingerprint=FinanceLedger.Hash(new{c.Id,c.Action,c.Reason,c.TargetId,c.SourceKey,c.Date,c.Rate,c.Time,c.Accrual,c.Recurring,c.CloseTask,c.BankTransactions,c.Splits,c.Asset,c.Vendor,c.ActivityEntryId,c.OffsetAccount,c.BankCsv,c.CashAccountId,c.Enabled,c.BankConnection});
        var old=s.Audit.SingleOrDefault(a=>a.CommandId==c.Id);
        if(old is not null){if(old.Fingerprint!=fingerprint)throw new ArgumentException("Command identity conflicts with prior content.");return new{Version=s.Version,Replayed=true};}
        if(s.Version!=c.ExpectedVersion)throw new InvalidOperationException("Finance changed. Refresh before retrying.");
        if(s.Accounts.Count==0)throw new ArgumentException("Initialize Finance first.");
        if(c.Action is "submit-time" or "import-activity-time" or "labor-rate")
        {
            var employee=c.Action=="labor-rate"?c.Rate?.EmployeeId:c.Time?.EmployeeId;
            var active=await memberships.GetActiveAssignedByTenantAsync(user.TenantId,ct);
            if(employee is null||!active.Any(m=>m.TenantId==user.TenantId&&m.UserId==employee&&!m.IsDeleted))throw new ArgumentException("Employee must be an active member of this tenant.");
            if(c.Action!="labor-rate")
            {
                var time=c.Time??throw new ArgumentException("Time payload required.");
                if(! (await jobs.GetActiveAsync(ct)).Any(j=>j.Id==time.JobId))throw new ArgumentException("Job must belong to this tenant.");
                if(c.Action=="import-activity-time")
                {
                    var partition=MedInsights.Lib.Utils.RepositoryKeyHelper.BuildPartitionKey(user.TenantId,time.Date.ToDateTime(TimeOnly.MinValue));
                    var activity=(await activities.GetForMonthAsync(partition,ct)).SingleOrDefault(a=>a.Id==c.ActivityEntryId&&a.TenantId==user.TenantId&&a.PartitionKey==partition&&!a.IsDeleted)??throw new ArgumentException("Source activity not found in tenant month.");
                    if(activity.UserId!=time.EmployeeId||DateOnly.FromDateTime(activity.EntryDate)!=time.Date||activity.Unit is not("hours" or "hour" or "h")||!double.IsFinite(activity.NumericValue))throw new ArgumentException("Source activity must be recorded employee hours on the selected date.");
                    var imported=time with{SourceKey="activity:"+activity.Id,Hours=(decimal)activity.NumericValue};
                    var command=new FinanceOperationsCommand{Action="submit-time",Time=imported};
                    FinanceOperationsEngine.Apply(s,command,user.UserId.ToString("D"));
                }
            }
        }
        var persistedLines=c.Action switch
        {
            "post-accrual-draft"=>s.Operations.AccrualDrafts.SingleOrDefault(a=>a.Id==c.TargetId)?.Lines??[],
            "reverse-accrual"=>s.Operations.Accruals.SingleOrDefault(a=>a.Id==c.TargetId)?.Lines??[],
            "post-recurring"=>s.Operations.Recurring.SingleOrDefault(a=>a.Id==c.TargetId)?.Lines??[],
            "generate-due-reversals"=>s.Operations.Accruals.Where(a=>a.ReversalJournalId is null&&a.ReversalDate<=c.Date).SelectMany(a=>a.Lines).ToArray(),
            _=>Array.Empty<FinanceLine>()
        };
        var allLines=(c.Accrual?.Lines??[]).Concat(c.Recurring?.Lines??[]).Concat(persistedLines).ToArray();
        var referencedJobs=allLines.Where(l=>l.JobId.HasValue).Select(l=>l.JobId!.Value).ToList();
        if(c.Asset?.JobId is Guid assetJob)referencedJobs.Add(assetJob);
        var effectiveTime=c.Action is "approve-time" or "post-time"?s.Operations.Time.SingleOrDefault(t=>t.Id==c.TargetId):null;
        if(effectiveTime is not null)
        {
            referencedJobs.Add(effectiveTime.JobId);
            if(!(await memberships.GetActiveAssignedByTenantAsync(user.TenantId,ct)).Any(m=>m.TenantId==user.TenantId&&m.UserId==effectiveTime.EmployeeId&&!m.IsDeleted))throw new ArgumentException("Employee is no longer active in this tenant.");
        }
        foreach(var jobId in referencedJobs.Distinct())if(await jobs.GetAsync(jobId,ct) is not { } found||found.Id!=jobId)throw new ArgumentException("Referenced job is unavailable in this tenant.");
        if(allLines.Any(l=>l.CustomerId is not null||l.VendorId is not null))throw new ArgumentException("Accrual and recurring journals cannot supply unchecked party dimensions.");
        var vendorId=c.Action=="vendor-compliance"?c.Vendor?.VendorId:c.Action=="capitalize-asset"?c.Asset?.VendorId:null;
        if(vendorId is not null&&! (await supply.ReadAsync(user.TenantId,ct)).Vendors.Any(v=>v.ContactId==vendorId&&v.Active))throw new ArgumentException("Select an active tenant vendor.");
        var actor=user.UserId.ToString("D");
        if(c.Action=="pull-bank-feed")
        {
            var connection=s.Operations.BankConnection??throw new ArgumentException("Configure tenant bank connection first.");
            var batch=await (bankProvider??throw new InvalidOperationException("Bank provider unavailable.")).ReadAsync(user.TenantId,connection,ct);
            if(batch.Transactions.Length>0)FinanceOperationsEngine.Apply(s,new(){Action="ingest-bank",SourceKey=batch.ProviderAccountKey,BankTransactions=batch.Transactions},actor);
        }
        else if(c.Action=="ingest-bank-ofx")
        {
            var batch=FinanceBankFeedOfx.Parse(c.BankCsv??throw new ArgumentException("OFX/QFX statement required."),c.SourceKey,c.CashAccountId);
            FinanceOperationsEngine.Apply(s,new(){Action="ingest-bank",SourceKey=batch.ProviderAccountKey,BankTransactions=batch.Transactions},actor);
        }
        else if(c.Action=="ingest-bank-csv")
        {
            var batch=FinanceBankFeedCsv.Parse(c.BankCsv??throw new ArgumentException("CSV required."),c.SourceKey,c.CashAccountId);
            FinanceOperationsEngine.Apply(s,new(){Action="ingest-bank",SourceKey=batch.ProviderAccountKey,BankTransactions=batch.Transactions},actor);
        }
        else if(c.Action!="import-activity-time")FinanceOperationsEngine.Apply(s,c,actor);
        s.Audit.Add(new(c.Id,"operations:"+c.Action,c.TargetId.ToString(),actor,DateTime.UtcNow,c.Reason,fingerprint));
        await store.SaveAsync(user.TenantId,s,c.ExpectedVersion,ct);return new{Version=(await store.ReadAsync(user.TenantId,ct)).Version,Replayed=false};
    }
}
