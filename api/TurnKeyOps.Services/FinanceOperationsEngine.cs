using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public static class FinanceOperationsEngine
{
    public static void ValidateTransition(FinanceState before,FinanceState after)
    {
        Preserve(before.Operations.ReversalAttempts,after.Operations.ReversalAttempts,x=>x.Id);
        Preserve(before.Operations.ApRetainageEvents,after.Operations.ApRetainageEvents,x=>x.Id);
        if(before.Bills.Any(b=>b.JournalId is not null&&b.RetainageAmount>0)&&before.Operations.ApRetainageAccount!=after.Operations.ApRetainageAccount)throw new ArgumentException("Used AP retainage mapping is immutable.");
        Preserve(before.Operations.Rates,after.Operations.Rates,x=>x.Id);
        Preserve(before.Operations.Time.Where(x=>x.Status=="POSTED"),after.Operations.Time,x=>x.Id);
        foreach(var old in before.Operations.Time.Where(t=>t.Status=="APPROVED"))
        {
            var next=after.Operations.Time.SingleOrDefault(t=>t.Id==old.Id);
            if(next is null||next.Status is not("APPROVED" or "POSTED")||FinanceLedger.Hash(old with{Status="",JournalId=null})!=FinanceLedger.Hash(next with{Status="",JournalId=null}))throw new ArgumentException("Approved time snapshots are immutable.");
        }
        Preserve(before.Operations.BankSplits,after.Operations.BankSplits,x=>(x.BankTransactionId,x.JournalId));
        Preserve(before.Operations.Assets,after.Operations.Assets,x=>x.Id);
        Preserve(before.Operations.Recurring,after.Operations.Recurring,x=>x.Id);
        foreach(var old in before.Operations.Accruals)
        {
            var next=after.Operations.Accruals.SingleOrDefault(x=>x.Id==old.Id);
            if(next is null||FinanceLedger.Hash(old with{ReversalJournalId=null})!=FinanceLedger.Hash(next with{ReversalJournalId=null})||old.ReversalJournalId is not null&&old.ReversalJournalId!=next.ReversalJournalId)throw new ArgumentException("Accrual history is immutable.");
        }
    }
    private static void Preserve<T,K>(IEnumerable<T> before,IEnumerable<T> after,Func<T,K> key) where K:notnull
    {
        var values=after.ToDictionary(key);
        foreach(var old in before)if(!values.TryGetValue(key(old),out var next)||FinanceLedger.Hash(old)!=FinanceLedger.Hash(next))throw new ArgumentException("Posted operations history is immutable.");
    }
    private static void Open(FinanceState s, DateOnly date)
    { if(date==default||!s.Periods.Any(p=>p.Start<=date&&p.End>=date&&p.Status=="OPEN"))throw new ArgumentException("An open accounting period is required."); }
    private static T Required<T>(T? value) where T:class => value??throw new ArgumentException("Operation payload required.");
    public static string[] CloseIssues(FinanceState s, FinancePeriod period)
    {
        var issues=new List<string>();
        if(s.Operations.Time.Any(t=>t.Date<=period.End&&t.Status is not("POSTED" or "REJECTED")))issues.Add("Unapproved or unposted time blocks close.");
        if(s.Operations.AccrualDrafts.Any(a=>a.Date<=period.End))issues.Add("Unposted accrual drafts block close.");
        if(s.Operations.Accruals.Any(a=>a.ReversalDate<=period.End&&a.ReversalJournalId is null))issues.Add("Due accrual reversals block close.");
        if(s.Operations.CloseTasks.Any(t=>t.PeriodId==period.Id&&t.Required&&!t.Complete))issues.Add("Required close checklist tasks are incomplete.");
        return issues.ToArray();
    }
    public static void Apply(FinanceState s, FinanceOperationsCommand c, string actor)
    {
        var o=s.Operations;
        switch(c.Action)
        {
            case "ap-retainage-policy":
                if(s.Bills.Any(b=>b.JournalId is not null&&b.RetainageAmount>0)&&o.ApRetainageAccount!=c.OffsetAccount)throw new ArgumentException("Used AP retainage mapping is immutable.");
                o.ApRetainageAccount=c.OffsetAccount;FinanceApRetainage.Account(s);break;
            case "reversal-policy":o.ReversalPolicy=new(c.Enabled,actor,DateTime.UtcNow);break;
            case "bank-connection":
                var connection=Required(c.BankConnection);FinanceLedger.Required(connection.ProviderId);FinanceLedger.Required(connection.AccountReference);
                if(!s.CashAccounts.Any(a=>a.Id==connection.CashAccountId&&a.Active))throw new ArgumentException("Select an active cash account.");o.BankConnection=connection with{ApprovedBy=actor,ApprovedAtUtc=DateTime.UtcNow};break;
            case "labor-rate":
                var r=Required(c.Rate);if(r.Id==Guid.Empty||r.EmployeeId==Guid.Empty||r.EffectiveDate==default||r.HourlyRate<=0||r.BurdenRate<0)throw new ArgumentException("Valid employee, effective date and positive rate required.");
                FinanceLedger.Money(r.HourlyRate);FinanceLedger.Money(r.BurdenRate);
                if(o.Rates.Any(x=>x.Id==r.Id||x.EmployeeId==r.EmployeeId&&x.EffectiveDate==r.EffectiveDate))throw new ArgumentException("Rate identity/effective date already exists.");
                o.Rates.Add(r with{Actor=actor});break;
            case "submit-time":
                var t=Required(c.Time);Open(s,t.Date);FinanceLedger.Required(t.SourceKey);
                if(t.EarningsClass is not("REGULAR" or "OVERTIME" or "DOUBLE_TIME"))throw new ArgumentException("Supported earnings classification required.");
                if(t.Id==Guid.Empty||t.EmployeeId==Guid.Empty||t.JobId==Guid.Empty||t.Hours<=0||t.Hours>24||t.Hours!=Math.Round(t.Hours,2)||!s.CostCodes.Any(x=>x.Code==t.CostCode&&x.Category=="LABOR"&&x.Active))throw new ArgumentException("Valid employee/job, labor cost code and hours up to 24 required.");
                if(o.Time.Any(x=>x.Id==t.Id||x.SourceKey==t.SourceKey))throw new ArgumentException("Duplicate time entry.");
                if(o.Time.Where(x=>x.EmployeeId==t.EmployeeId&&x.Date==t.Date&&x.Status!="REJECTED").Sum(x=>x.Hours)+t.Hours>24)throw new ArgumentException("Daily employee hours exceed 24.");
                o.Time.Add(t with{Status="SUBMITTED",RateId=null,RateSnapshot=null,JournalId=null,ApprovedBy=null,BaseRateSnapshot=null,BurdenRateSnapshot=null});break;
            case "approve-time":
                var ix=o.Time.FindIndex(x=>x.Id==c.TargetId);if(ix<0)throw new ArgumentException("Time entry not found.");
                var entry=o.Time[ix];Open(s,entry.Date);if(entry.Status!="SUBMITTED")throw new ArgumentException("Time is already approved.");
                var rate=o.Rates.Where(x=>x.EmployeeId==entry.EmployeeId&&x.EffectiveDate<=entry.Date).OrderByDescending(x=>x.EffectiveDate).FirstOrDefault()??throw new ArgumentException("Missing effective labor cost rate.");
                var multiplier=entry.EarningsClass=="OVERTIME"?1.5m:entry.EarningsClass=="DOUBLE_TIME"?2m:1m;
                o.Time[ix]=entry with{Status="APPROVED",RateId=rate.Id,RateSnapshot=rate.HourlyRate*multiplier+rate.BurdenRate,BaseRateSnapshot=rate.HourlyRate*multiplier,BurdenRateSnapshot=rate.BurdenRate,ApprovedBy=actor};break;
            case "reject-time":
                var rejected=o.Time.FindIndex(x=>x.Id==c.TargetId&&x.Status=="SUBMITTED");if(rejected<0)throw new ArgumentException("Submitted time required.");FinanceLedger.Required(c.Reason);o.Time[rejected]=o.Time[rejected] with{Status="REJECTED",ApprovedBy=actor};break;
            case "post-time":
                var postedIndex=o.Time.FindIndex(x=>x.Id==c.TargetId&&x.Status=="APPROVED");if(postedIndex<0)throw new ArgumentException("Approved time required.");
                var approved=o.Time[postedIndex];Open(s,approved.Date);
                var amount=Math.Round(approved.Hours*approved.RateSnapshot!.Value,2,MidpointRounding.AwayFromZero);
                Liability(s,c.OffsetAccount);
                var journal=FinanceLedger.Post(s,approved.Date,"labor-time",approved.Id.ToString(),"Approved labor time",[new(s.Policy.Accounts["cost"],amount,0,approved.JobId,approved.CostCode,Category:"LABOR"),new(c.OffsetAccount,0,amount)],actor);
                s.Costs.Add(new(Guid.NewGuid(),approved.JobId,"LABOR",approved.CostCode,amount,approved.Date,"labor-time",approved.Id.ToString(),journal.Id,approved.Hours,approved.RateSnapshot));
                o.Time[postedIndex]=approved with{Status="POSTED",JournalId=journal.Id};break;
            case "draft-accrual":
                var draft=Required(c.Accrual);Open(s,draft.Date);if(draft.Id==Guid.Empty||o.AccrualDrafts.Any(x=>x.Id==draft.Id)||o.Accruals.Any(x=>x.Id==draft.Id)||draft.ReversalDate<=draft.Date)throw new ArgumentException("Unique draft and later reversal date required.");ValidateNonControl(s,draft.Lines);o.AccrualDrafts.Add(draft with{JournalId=Guid.Empty,ReversalJournalId=null});break;
            case "post-accrual-draft":
                var pending=o.AccrualDrafts.SingleOrDefault(x=>x.Id==c.TargetId)??throw new ArgumentException("Accrual draft not found.");
                Apply(s,new(){Action="post-accrual",Accrual=pending},actor);o.AccrualDrafts.Remove(pending);break;
            case "generate-due-reversals":
                if(c.Date==default)throw new ArgumentException("As-of date required.");foreach(var due in o.Accruals.Where(x=>x.ReversalJournalId is null&&x.ReversalDate<=c.Date).ToArray())Apply(s,new(){Action="reverse-accrual",TargetId=due.Id},actor);break;
            case "post-accrual":
                var accrual=Required(c.Accrual);if(accrual.Id==Guid.Empty||o.Accruals.Any(x=>x.Id==accrual.Id)||accrual.ReversalDate<=accrual.Date)throw new ArgumentException("Unique accrual and later reversal date required.");
                ValidateNonControl(s,accrual.Lines);
                var aj=FinanceLedger.Post(s,accrual.Date,"accrual",accrual.Id.ToString(),accrual.Description,accrual.Lines,actor);
                AddJobCosts(s,aj);o.Accruals.Add(accrual with{JournalId=aj.Id,ReversalJournalId=null});break;
            case "reverse-accrual":
                var ai=o.Accruals.FindIndex(x=>x.Id==c.TargetId);if(ai<0)throw new ArgumentException("Accrual not found.");var a=o.Accruals[ai];
                if(a.ReversalJournalId is not null)throw new ArgumentException("Accrual already reversed.");
                var reversal=FinanceLedger.Post(s,a.ReversalDate,"accrual-reversal",a.Id.ToString(),"Reverse "+a.Description,a.Lines.Select(l=>l with{Debit=l.Credit,Credit=l.Debit}).ToArray(),actor,a.JournalId);
                AddJobCosts(s,reversal);o.Accruals[ai]=a with{ReversalJournalId=reversal.Id};break;
            case "recurring-template":
                var template=Required(c.Recurring);if(template.Id==Guid.Empty||template.Start==default||template.End<template.Start||o.Recurring.Any(x=>x.Id==template.Id))throw new ArgumentException("Valid unique recurring schedule required.");
                ValidateNonControl(s,template.Lines);o.Recurring.Add(template);break;
            case "post-recurring":
                var recurring=o.Recurring.SingleOrDefault(x=>x.Id==c.TargetId)??throw new ArgumentException("Template not found.");
                if(c.Date<recurring.Start||c.Date>recurring.End)throw new ArgumentException("Posting date outside schedule.");
                var recurringSource=recurring.Id+":"+c.Date.ToString("yyyy-MM");
                var existed=s.Journals.Any(j=>j.SourceType=="recurring"&&j.SourceId==recurringSource);
                var recurringJournal=FinanceLedger.Post(s,c.Date,"recurring",recurringSource,recurring.Description,recurring.Lines,actor);
                if(!existed)AddJobCosts(s,recurringJournal);break;
            case "close-task":
                var task=Required(c.CloseTask);var period=s.Periods.SingleOrDefault(p=>p.Id==task.PeriodId)??throw new ArgumentException("Period not found.");Open(s,period.End);FinanceLedger.Required(task.Key);FinanceLedger.Required(task.Description);
                var old=o.CloseTasks.SingleOrDefault(x=>x.PeriodId==task.PeriodId&&x.Key==task.Key);if(old is not null)o.CloseTasks.Remove(old);o.CloseTasks.Add(task with{Actor=actor});break;
            case "ingest-bank": Ingest(s,c);break;
            case "match-bank-split": Match(s,c,actor);break;
            case "capitalize-asset":
                var asset=Required(c.Asset);if(asset.Id==Guid.Empty||o.Assets.Any(x=>x.Id==asset.Id))throw new ArgumentException("Unique asset identity required.");FinanceLedger.Required(asset.Description);FinanceLedger.Positive(asset.Cost);
                if(!s.Accounts.Any(x=>x.Code==c.SourceKey&&x.Type=="ASSET"&&x.Active)||s.CashAccounts.Any(x=>x.LedgerAccount==c.SourceKey)||c.SourceKey==s.Policy.Accounts["ar"]||c.SourceKey==s.Policy.Accounts["inventory"]||s.Construction.Accounts.Values.Contains(c.SourceKey))throw new ArgumentException("Select a dedicated fixed asset account.");
                var source=s.Journals.SingleOrDefault(j=>j.Id==c.TargetId)??throw new ArgumentException("Select the recorded equipment purchase journal for capitalization.");
                if(!s.Accounts.Any(x=>x.Code==c.OffsetAccount&&x.Type=="EXPENSE")||source.Lines.Where(l=>l.Account==c.OffsetAccount).Sum(l=>l.Debit-l.Credit)<asset.Cost||o.Assets.Any(x=>x.EquipmentId==source.Id.ToString()))throw new ArgumentException("Capitalization must reclassify an unused recorded expense purchase.");
                asset=asset with{EquipmentId=source.Id.ToString()};
                ValidateNonControl(s,[new(c.OffsetAccount,0,asset.Cost)]);
                var assetJournal=FinanceLedger.Post(s,asset.AcquisitionDate,"fixed-asset",asset.Id.ToString(),asset.Description,[new(c.SourceKey,asset.Cost,0),new(c.OffsetAccount,0,asset.Cost)],actor);
                o.Assets.Add(asset with{Status="ACTIVE",JournalId=assetJournal.Id});break;
            case "vendor-compliance":
                var vendor=Required(c.Vendor);if(vendor.VendorId==Guid.Empty||vendor.W9Status is not("MISSING" or "REQUESTED" or "RECEIVED"))throw new ArgumentException("Valid vendor/W-9 status required.");
                o.Vendors.RemoveAll(x=>x.VendorId==vendor.VendorId);o.Vendors.Add(vendor);break;
            default:throw new ArgumentException("Unknown Finance operation.");
        }
    }
    private static void Liability(FinanceState s,string code)
    {if(!s.Accounts.Any(x=>x.Code==code&&x.Active&&x.Type=="LIABILITY")||code==s.Policy.Accounts["ap"]||code==s.Policy.Accounts["tax"]||s.Construction.Accounts.Values.Contains(code)||code==s.Operations.ApRetainageAccount)throw new ArgumentException("Select a dedicated accrued liability account.");}
    private static void ValidateNonControl(FinanceState s,FinanceLine[] lines)
    {if(lines.Any(l=>l.Account==s.Policy.Accounts["ar"]||l.Account==s.Policy.Accounts["ap"]||s.Construction.Accounts.Values.Contains(l.Account)||l.Account==s.Operations.ApRetainageAccount))throw new ArgumentException("AR/AP control entries require their subledger workflow.");}
    private static void AddJobCosts(FinanceState s, FinanceJournal journal)
    {
        foreach(var line in journal.Lines.Where(l=>l.JobId is not null&&s.Accounts.Any(a=>a.Code==l.Account&&a.Type=="COST_OF_GOODS_SOLD")))
        {if(line.Category is null||!FinanceLedger.Categories.Contains(line.Category))throw new ArgumentException("Job accrual/recurring costs require a supported category.");s.Costs.Add(new(Guid.NewGuid(),line.JobId!.Value,line.Category,line.CostCode,line.Debit-line.Credit,journal.Date,journal.SourceType,journal.SourceId,journal.Id));}
    }
    private static void Ingest(FinanceState s,FinanceOperationsCommand c)
    {
        FinanceLedger.Required(c.SourceKey);
        if(c.BankTransactions is null||c.BankTransactions.Length is <1 or >10000)throw new ArgumentException("Bank feed requires 1–10000 transactions.");
        foreach(var b in c.BankTransactions)
        {
            FinanceLedger.Required(b.ExternalId);FinanceLedger.Money(b.Amount);if(b.Date==default||b.Amount==0||!s.CashAccounts.Any(x=>x.Id==b.CashAccountId&&x.Active))throw new ArgumentException("Valid bank transaction required.");
            var external=c.SourceKey+":"+b.ExternalId;
            var old=s.BankTransactions.SingleOrDefault(x=>x.CashAccountId==b.CashAccountId&&x.ExternalId==external);
            if(old is not null){if(old.Date!=b.Date||old.Amount!=b.Amount||old.Reference!=b.Reference)throw new ArgumentException("External transaction conflicts with existing feed content.");continue;}
            if(s.Reconciliations.Any(x=>x.CashAccountId==b.CashAccountId&&b.Date<=x.End))throw new ArgumentException("Bank interval already reconciled.");
            Open(s,b.Date);s.BankTransactions.Add(new(){CashAccountId=b.CashAccountId,ExternalId=external,ImportId=c.SourceKey,Date=b.Date,Amount=b.Amount,Reference=b.Reference});
        }
    }
    private static void Match(FinanceState s,FinanceOperationsCommand c,string actor)
    {
        FinanceLedger.Required(c.Reason);var bank=s.BankTransactions.SingleOrDefault(x=>x.Id==c.TargetId)??throw new ArgumentException("Bank transaction not found.");Open(s,bank.Date);
        if(bank.JournalId is not null||bank.ReconciliationId is not null||s.Operations.BankSplits.Any(x=>x.BankTransactionId==bank.Id)||c.Splits.Length==0||c.Splits.Select(x=>x.JournalId).Distinct().Count()!=c.Splits.Length||c.Splits.Sum(x=>x.Amount)!=bank.Amount)throw new ArgumentException("Match must allocate entire unmatched bank amount to distinct journals.");
        var account=s.CashAccounts.Single(x=>x.Id==bank.CashAccountId).LedgerAccount;
        foreach(var split in c.Splits)
        {
            FinanceLedger.Money(split.Amount);var journal=s.Journals.SingleOrDefault(x=>x.Id==split.JournalId)??throw new ArgumentException("Journal not found.");
            var cash=journal.Lines.Where(x=>x.Account==account).Sum(x=>x.Debit-x.Credit);
            var allocated=s.Operations.BankSplits.Where(x=>x.JournalId==journal.Id&&s.BankTransactions.Any(b=>b.Id==x.BankTransactionId&&b.CashAccountId==bank.CashAccountId)).Sum(x=>x.Amount);
            if(s.BankTransactions.Any(x=>x.CashAccountId==bank.CashAccountId&&x.JournalId==journal.Id)||split.Amount==0||Math.Sign(split.Amount)!=Math.Sign(cash)||Math.Abs(allocated+split.Amount)>Math.Abs(cash))throw new ArgumentException("Cash match exceeds available journal amount.");
        }
        s.Operations.BankSplits.AddRange(c.Splits.Select(x=>x with{BankTransactionId=bank.Id,Actor=actor}));bank.ConfirmedBy=actor;
    }
}
