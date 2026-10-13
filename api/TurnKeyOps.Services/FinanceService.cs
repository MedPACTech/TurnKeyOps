using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;

public sealed class FinanceService(IFinanceStore store, IFinanceAuthority authority, IUserContext user,
    IInvoiceService invoices, IJobService jobs, ISupplyStore supplyStore, TurnKeyOps.Repositories.Interfaces.ICustomerRepository customers, IQuoteEstimateService? estimates=null,
    IFinanceSourceCoordinator? coordinator=null, FinanceInvoiceDispatch? dispatch=null)
{
    public async Task<object> WorkspaceAsync(DateOnly? from=null,DateOnly? asOf=null,CancellationToken ct=default)
    {
        await authority.RequireAsync(false,ct);
        var s=await store.ReadAsync(user.TenantId,ct);
        var end=asOf??DateOnly.FromDateTime(DateTime.UtcNow);var start=from??new DateOnly(end.Year,end.Month,1);
        if(start>end)throw new ArgumentException("Report start must precede as-of date.");
        var bankSuggestions=s.BankTransactions.Where(b=>b.JournalId is null&&!s.Operations.BankSplits.Any(x=>x.BankTransactionId==b.Id)).Select(b=>new{b.Id,Candidates=BankCandidates(s,b)}).ToArray();
        var closeReadiness=s.Periods.Select(p=>new{p.Id,Issues=FinanceLedger.CloseIssues(s,p)}).ToArray();
        var canControl=await authority.CanControlAsync(ct);
        if(!canControl)
        {
            s=JobConfigurationService.Clone(s);
            s.Operations=new FinanceOperationsState();
            s.Costs=s.Costs.Select(c=>c with{Rate=null,Hours=null}).ToList();
        }
        var supply=await supplyStore.ReadAsync(user.TenantId,ct);
        var jobChoices=(await jobs.GetActiveAsync(ct)).Select(j=>new{j.Id,j.Name}).ToArray();
        return new{JobChoices=jobChoices,Vendors=supply.Vendors.Where(v=>v.Active).Select(v=>new{Id=v.ContactId,v.Name}).ToArray(),Orders=supply.Orders.Select(o=>new{o.Id,o.Number,o.VendorId,Lines=o.Lines.Select(l=>new{l.Id,l.Description,l.Quantity,l.UnitCost}).ToArray()}).ToArray(),State=s,Reports=FinanceLedger.Reports(s,start,end),CanWrite=await authority.CanWriteAsync(ct),CanControl=canControl,
            Jobs=s.Budgets.Select(b=>JobHealth(s,supply,b,end)).ToArray(),Forecast=FinanceInsights.Forecast(s,end),InvoiceDispatch=dispatch is null?null:await dispatch.GetStatusAsync(user.TenantId,ct),
            Close=closeReadiness,
            Suggestions=bankSuggestions};
    }
    public async Task<object> AnalyzeAsync(string tool,JsonElement input,CancellationToken ct=default)
    {
        await authority.RequireAsync(false,ct);
        var date=DateOnly.FromDateTime(DateTime.UtcNow);
        if(input.ValueKind==JsonValueKind.Object&&input.TryGetProperty("asOf",out var selected))
        {
            if(selected.ValueKind!=JsonValueKind.String||!DateOnly.TryParseExact(selected.GetString(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out date))throw new ArgumentException("Use an ISO yyyy-MM-dd as-of date.");
        }
        return FinanceInsights.Review(await store.ReadAsync(user.TenantId,ct),await supplyStore.ReadAsync(user.TenantId,ct),tool,date);
    }
    public static object JobHealth(FinanceState s,SupplyState supply,FinanceJobBudget b,DateOnly asOf)
    {
        var actual=s.Costs.Where(c=>c.JobId==b.JobId&&c.Date<=asOf).ToArray();
        var committed=FinanceMigrationCommitments.Remaining(s,supply,b.JobId,asOf);
        var revenue=s.Journals.Where(j=>j.Date<=asOf).SelectMany(j=>j.Lines).Where(l=>l.JobId==b.JobId&&s.Accounts.Any(a=>a.Code==l.Account&&a.Type is "REVENUE" or "OTHER_INCOME")).Sum(l=>l.Credit-l.Debit);
        var total=actual.Sum(c=>c.Amount);var contract=b.Sold+b.ApprovedChanges;
        var complete=b.TaxBasisKnown&&b.ActualsComplete&&b.Categories.Values.All(v=>v.HasValue)&&!actual.Any(c=>c.SourceType=="migration-job-cost")&&!FinanceBillExtraAllocations.Outstanding(s,b.JobId,asOf);
        return new{b.JobId,ContractValue=b.Sold,b.ApprovedChanges,CurrentValue=contract,Budget=b.Categories,Committed=committed,CommitmentSources=FinanceInsights.Commitments(s,supply,b.JobId,asOf),ImportedCommitmentSources=s.ImportedCommitments.Where(c=>c.JobId==b.JobId&&c.Date<=asOf).ToArray(),Actual=total,Revenue=revenue,Categories=actual.GroupBy(c=>c.Category).Select(g=>new{Category=g.Key,Amount=g.Sum(c=>c.Amount)}).ToArray(),GrossProfit=complete?(decimal?)(revenue-total):null,GrossMargin=complete&&revenue!=0?(decimal?)((revenue-total)/revenue*100):null,CostDataIncomplete=!complete,CompletenessNote=FinanceBillExtraAllocations.Outstanding(s,b.JobId,asOf)?"Bill tax or shipping remains unallocated; profitability is unknown. "+b.CompletenessNote:actual.Any(c=>c.SourceType=="migration-job-cost")?"Historical earned revenue is not mapped; profitability remains unknown. "+b.CompletenessNote:b.CompletenessNote,SourceIds=actual.Select(c=>new{c.SourceType,c.SourceId,c.JournalId}).ToArray()};
    }
    public static object[] BankCandidates(FinanceState s,FinanceBankTransaction b)
    {
        var account=s.CashAccounts.Single(a=>a.Id==b.CashAccountId);
        return s.Journals.Where(j=>Math.Abs(j.Date.DayNumber-b.Date.DayNumber)<=3&&j.Lines.Where(l=>l.Account==account.LedgerAccount).Sum(l=>l.Debit-l.Credit)==b.Amount&&!s.BankTransactions.Any(t=>t.CashAccountId==b.CashAccountId&&t.JournalId==j.Id)&&!s.Operations.BankSplits.Any(x=>x.JournalId==j.Id&&s.BankTransactions.Any(t=>t.Id==x.BankTransactionId&&t.CashAccountId==b.CashAccountId))).Select(j=>(object)new{j.Id,j.Date,j.Memo,j.SourceType,j.SourceId,Confidence=j.Date==b.Date&&j.SourceId==b.Reference?"exact":"review required"}).ToArray();
    }
    public async Task<object> CommandAsync(FinanceCommand c,CancellationToken ct=default)
    {
        await authority.RequireAsync(true,ct);
        if(c.Id==Guid.Empty)throw new ArgumentException("Command identity is required.");
        if(c.Action is "initialize" or "period" or "close" or "reopen" or "policy" or "post-journal" or "reverse" or "approve-bill" or "opening-import" or "reconcile" or "post-draft" or "budget-review" or "cost-completeness" or "correct-job-change" or "allocate-bill-extras" or "release-ap-retainage" or "credit-retained-bill")
            if(!await authority.CanControlAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("An authorized Finance owner must explicitly perform this control action.");
        await using var sourceLease=coordinator is not null&&c.Action is ("close" or "sync-invoices" or "opening-import" or "payment" or "refund" or "credit")?await coordinator.AcquireAsync(user.TenantId,ct):null;
        var s=await store.ReadAsync(user.TenantId,ct);var expected=c.ExpectedVersion;
        var fingerprint=FinanceLedger.Hash(new{c.Id,c.Action,c.Target,c.Reason,c.Date,c.EndDate,c.Amount,c.OpeningBalance,c.Reference,c.Method,c.PartyId,c.JobId,c.Category,c.CostCode,c.Account,c.SourceKey,c.Hours,c.Rate,c.Lines,c.Allocations,c.ChartAccount,c.Bill,c.Code,c.CashAccount,c.BankTransactions,c.Policy,c.Budget,c.Complete,c.ArControl,c.ApControl,c.ImportOpenItems});
        var old=s.Audit.SingleOrDefault(a=>a.CommandId==c.Id);
        if(old is not null){if(old.Fingerprint!=fingerprint)throw new ArgumentException("Command identity was reused with different content.");return new{Version=s.Version,Replayed=true};}
        if(expected!=s.Version)throw new InvalidOperationException("Finance changed. Refresh before retrying.");
        var actor=user.UserId.ToString("D");
        if(c.Action is "post-journal" or "draft-journal" && c.Lines.Any(l=>(s.Construction.Accounts.Values.Contains(l.Account)||l.Account==s.Operations.ApRetainageAccount)))throw new ArgumentException("Use construction subledger commands for construction control accounts.");
        if(c.Action=="post-draft" && s.JournalDrafts.Where(d=>d.Id.ToString()==c.Target).SelectMany(d=>d.Lines).Any(l=>(s.Construction.Accounts.Values.Contains(l.Account)||l.Account==s.Operations.ApRetainageAccount)))throw new ArgumentException("Construction control accounts require source transactions.");
        if(c.Action!="initialize"&&s.Accounts.Count==0)throw new ArgumentException("Initialize Finance first.");
        switch(c.Action)
        {
            case "initialize": FinanceLedger.Initialize(s);break;
            case "account": UpsertAccount(s,c.ChartAccount??throw new ArgumentException("Account required."));break;
            case "period":
                if(c.Date==default||c.Date.Day!=1||c.EndDate!=c.Date.AddMonths(1).AddDays(-1)||s.Periods.Any(p=>c.Date<=p.End&&c.EndDate>=p.Start))throw new ArgumentException("Create a non-overlapping calendar month.");
                s.Periods.Add(new(){Id=c.Date.ToString("yyyy-MM"),Start=c.Date,End=c.EndDate});break;
            case "close":case "reopen":
                FinanceLedger.Required(c.Reason);var period=s.Periods.SingleOrDefault(p=>p.Id==c.Target)??throw new ArgumentException("Period not found.");
                if(c.Action=="close"){await SyncInvoices(s,actor,ct);var issues=FinanceLedger.CloseIssues(s,period);if(issues.Length>0)throw new ArgumentException(string.Join(" ",issues));}
                period.Status=c.Action=="close"?"CLOSED":"OPEN";break;
            case "policy":
                if(s.Journals.Count>0)throw new ArgumentException("Posting mappings cannot change after go-live.");
                var policy=c.Policy??throw new ArgumentException("Policy required.");
                if(policy.Currency!="USD"||policy.MaterialActualSource!="vendor-bill"||policy.RevenueRecognition!="invoice"||policy.Matching is not ("strict" or "warning" or "disabled"))throw new ArgumentException("Unsupported V1 accounting policy.");
                foreach(var pair in s.Policy.Accounts){if(!policy.Accounts.TryGetValue(pair.Key,out var mappedCode)||!s.Accounts.Any(a=>a.Code==mappedCode&&a.Active))throw new ArgumentException("All posting roles require active accounts.");}
                if(policy.Accounts.Values.Distinct().Count()!=policy.Accounts.Count)throw new ArgumentException("Control and posting accounts must be distinct.");
                var roleTypes=new Dictionary<string,string>{["cash"]="ASSET",["ar"]="ASSET",["inventory"]="ASSET",["ap"]="LIABILITY",["tax"]="LIABILITY",["equity"]="EQUITY",["revenue"]="REVENUE",["cost"]="COST_OF_GOODS_SOLD"};
                if(roleTypes.Any(r=>!s.Accounts.Any(a=>a.Code==policy.Accounts[r.Key]&&a.Type==r.Value)))throw new ArgumentException("Posting accounts must match their accounting role.");
                var operating=s.CashAccounts.Single(a=>a.Id=="operating");
                if(s.CashAccounts.Any(a=>a.Id!="operating"&&a.LedgerAccount==policy.Accounts["cash"]))throw new ArgumentException("The posting cash account is already assigned to another cash account.");
                s.CashAccounts.Remove(operating);s.CashAccounts.Add(operating with{LedgerAccount=policy.Accounts["cash"]});
                s.Policy=policy;break;
            case "draft-journal":
                FinanceLedger.Required(c.Reason);if(c.Lines.Length is <2 or >500)throw new ArgumentException("Draft needs 2–500 lines.");
                s.JournalDrafts.Add(new(c.Id,c.Date,c.Reason,c.Lines,actor));break;
            case "post-draft":
                var journalDraft=s.JournalDrafts.SingleOrDefault(d=>d.Id.ToString()==c.Target&&d.PostedJournalId is null)??throw new ArgumentException("Unposted draft not found.");
                if(journalDraft.Lines.Any(l=>l.JobId is not null))throw new ArgumentException("Use an explicit Job cost transaction.");
                if(journalDraft.Lines.Any(l=>l.Account==s.Policy.Accounts["ar"]||l.Account==s.Policy.Accounts["ap"]))throw new ArgumentException("Use AR/AP source transactions.");
                var draftPosted=FinanceLedger.Post(s,journalDraft.Date,"manual",journalDraft.Id.ToString(),journalDraft.Memo,journalDraft.Lines,actor);
                s.JournalDrafts.Remove(journalDraft);s.JournalDrafts.Add(journalDraft with{PostedJournalId=draftPosted.Id});break;
            case "post-journal":
                if(c.Lines.Any(l=>l.JobId is not null))throw new ArgumentException("Use an explicit Job cost transaction for Job allocations.");
                if(c.Lines.Any(l=>l.Account==s.Policy.Accounts["ar"]||l.Account==s.Policy.Accounts["ap"]))throw new ArgumentException("Use subledger transactions for AR/AP control accounts.");
                FinanceLedger.Post(s,c.Date,"manual",c.Id.ToString(),FinanceLedger.Required(c.Reason),c.Lines,actor);break;
            case "reverse":
                var original=s.Journals.SingleOrDefault(j=>j.Id.ToString()==c.Target)??throw new ArgumentException("Journal not found.");
                if(original.SourceType is not ("manual" or "job-cost"))throw new ArgumentException("Correct source transactions through a credit or refund.");
                if(s.Journals.Any(j=>j.ReversesId==original.Id))throw new ArgumentException("Journal already reversed.");
                var reversal=FinanceLedger.Post(s,c.Date,"reversal",original.Id.ToString(),FinanceLedger.Required(c.Reason),original.Lines.Select(l=>l with{Debit=l.Credit,Credit=l.Debit}).ToArray(),actor,original.Id);
                foreach(var cost in s.Costs.Where(x=>x.JournalId==original.Id).ToArray())s.Costs.Add(cost with{Id=Guid.NewGuid(),Amount=-cost.Amount,Date=c.Date,SourceType="reversal",SourceId=original.Id.ToString(),JournalId=reversal.Id});break;
            case "sync-invoices": await SyncInvoices(s,actor,ct);break;
            case "bill":
                var bill=c.Bill??throw new ArgumentException("Bill required.");
                if(s.Bills.Any(b=>b.Id==bill.Id))throw new ArgumentException("Bill identity already exists. Create an explicit replacement after voiding the draft.");
                if(bill.VendorId==Guid.Empty||! (await supplyStore.ReadAsync(user.TenantId,ct)).Vendors.Any(v=>v.ContactId==bill.VendorId&&v.Active))throw new ArgumentException("Select an active tenant vendor in Purchasing.");
                FinanceLedger.Required(bill.Number);
                if(bill.Date==default||bill.DueDate<bill.Date||bill.Lines.Count==0||bill.Tax<0||bill.Shipping<0)throw new ArgumentException("Bill dates and amounts are invalid.");
                foreach(var line in bill.Lines){if(line.CostCode is not null&&!s.CostCodes.Any(code=>code.Code==line.CostCode&&code.Category==line.Category&&code.Active))throw new ArgumentException("Bill cost code must match its category.");FinanceLedger.Positive(line.Quantity);FinanceLedger.Money(line.UnitCost);if(line.UnitCost<0||!FinanceLedger.Categories.Contains(line.Category))throw new ArgumentException("Invalid bill cost.");if(line.JobId is Guid jobId)await RequireJob(jobId,ct);}
                FinanceApRetainage.ValidateBill(s,bill);
                FinanceLedger.Money(bill.Tax);FinanceLedger.Money(bill.Shipping);FinanceLedger.Positive(bill.Total);bill.Status="PENDING_APPROVAL";bill.JournalId=null;
                FinanceMigrationCommitments.ValidateBill(s,bill);
                bill.MatchExceptions=FinanceLedger.Match(bill,await supplyStore.ReadAsync(user.TenantId,ct),s.Bills);
                if(bill.MatchExceptions.Contains("Duplicate vendor invoice number."))throw new ArgumentException("Duplicate vendor invoice number.");
                s.Bills.Add(bill);break;
            case "allocate-bill-extras":
                foreach(var allocation in c.Lines){if(allocation.JobId is not Guid allocatedJob)throw new ArgumentException("Every bill-extra allocation requires a canonical Job.");await RequireJob(allocatedJob,ct);}
                FinanceBillExtraAllocations.Allocate(s,c,actor);break;
            case "approve-bill":
                var approved=s.Bills.SingleOrDefault(b=>b.Id.ToString()==c.Target&&b.Status=="PENDING_APPROVAL")??throw new ArgumentException("Bill not pending approval.");
                FinanceMigrationCommitments.ValidateBill(s,approved);
                approved.MatchExceptions=FinanceLedger.Match(approved,await supplyStore.ReadAsync(user.TenantId,ct),s.Bills);
                if(s.Policy.Matching=="strict"&&approved.MatchExceptions.Length>0)throw new ArgumentException(string.Join(" ",approved.MatchExceptions));
                if(s.Policy.Matching=="warning"&&approved.MatchExceptions.Length>0)FinanceLedger.Required(c.Reason);
                var billLines=approved.Lines.Select(l=>new FinanceLine(l.Account,l.Amount,0,l.JobId,l.CostCode,VendorId:approved.VendorId,Description:l.Description,Category:l.Category)).ToList();
                if(approved.Tax+approved.Shipping>0)billLines.Add(new(s.Policy.Accounts["cost"],approved.Tax+approved.Shipping,0,VendorId:approved.VendorId,Description:"Purchase tax and shipping (unallocated)"));
                FinanceApRetainage.ValidateBill(s,approved);
                if(approved.NetPayable>0)billLines.Add(new(s.Policy.Accounts["ap"],0,approved.NetPayable,VendorId:approved.VendorId));
                if(approved.RetainageAmount>0)billLines.Add(new(FinanceApRetainage.Account(s),0,approved.RetainageAmount,VendorId:approved.VendorId));
                if(approved.Lines.Any(l=>l.Account==s.Policy.Accounts["ar"]||l.Account==s.Policy.Accounts["ap"]||!s.Accounts.Any(a=>a.Code==l.Account&&a.Type is "EXPENSE" or "COST_OF_GOODS_SOLD")))throw new ArgumentException("V1 bills use expense/direct-cost accounts.");
                var posted=FinanceLedger.Post(s,approved.Date,"vendor-bill",approved.Id.ToString(),approved.Number,billLines.ToArray(),actor);
                approved.JournalId=posted.Id;approved.Status="APPROVED";if(approved.NetPayable>0)s.OpenItems.Add(new(approved.Id,"AP",approved.VendorId,approved.Number,approved.Date,approved.DueDate,approved.NetPayable,posted.Id,ExternalId:approved.ExternalId));
                foreach(var line in approved.Lines.Where(l=>l.JobId is not null))s.Costs.Add(new(Guid.NewGuid(),line.JobId!.Value,line.Category,line.CostCode,line.Amount,approved.Date,"vendor-bill",approved.Id.ToString(),posted.Id));break;
            case "release-ap-retainage":FinanceApRetainage.Apply(s,c,actor,false);break;
            case "credit-retained-bill":FinanceApRetainage.Apply(s,c,actor,true);break;
            case "void-bill":
                var draft=s.Bills.SingleOrDefault(b=>b.Id.ToString()==c.Target&&b.JournalId is null)??throw new ArgumentException("Only unposted bills can be voided.");FinanceLedger.Required(c.Reason);draft.Status="VOID";break;
            case "payment":case "credit":case "refund":case "vendor-payment":case "vendor-credit":case "vendor-refund":
                if(c.Action is "payment" or "refund" or "credit"&&c.Allocations.Any(a=>s.ConstructionPayments.Any(p=>p.OpenItemId==a.OpenItemId&&p.Status is "PENDING" or "READY" or "CANCEL_PENDING" or "RECEIVED")))throw new ArgumentException("Cancel or reconcile the active construction checkout before changing its receivable balance.");
                if(s.Settlements.Any(p=>p.SourceKey==c.SourceKey&&p.Kind==c.Action))throw new ArgumentException("Settlement source key already exists.");
                FinanceLedger.Settle(s,c,actor,c.Action);break;
            case "cost-code":
                var code=c.Code??throw new ArgumentException("Cost code required.");FinanceLedger.Required(code.Code);FinanceLedger.Required(code.Name);
                if(!FinanceLedger.Categories.Contains(code.Category))throw new ArgumentException("Unknown cost category.");
                var usedConstructionCode=s.Construction.CodeBudgets.Any(b=>b.Status=="REVIEWED"&&b.Lines.Any(l=>l.CostCode==code.Code))||s.Construction.CommitmentCodings.Any(coding=>coding.CostCode==code.Code)||s.Construction.Forecasts.Any(f=>f.Costs?.Any(cost=>cost.CostCode==code.Code)==true)||s.Construction.Contracts.Any(contract=>contract.Lines.Any(line=>line.CostCode==code.Code))||s.Construction.ContractRevisions.Any(revision=>revision.Contract.Lines.Any(line=>line.CostCode==code.Code));
                if(s.CostCodes.Any(x=>x.Code==code.Code)&&(s.Costs.Any(x=>x.CostCode==code.Code)||usedConstructionCode))throw new ArgumentException("Used cost codes cannot be redefined.");
                s.CostCodes.RemoveAll(x=>x.Code==code.Code);s.CostCodes.Add(code);break;
            case "seed-budget": await SeedBudget(s,c,ct);break;
            case "sync-job-changes": await SyncJobChanges(s,c,ct);break;
            case "correct-job-change":
                if(c.JobId is not Guid correctionJobId)throw new ArgumentException("Canonical Job required.");
                var correctionJob=await jobs.GetAsync(correctionJobId,ct)??throw new ArgumentException("Job is unavailable in this tenant.");
                FinanceContractCorrections.Cancel(s,c,correctionJob,actor);break;
            case "budget-review":
                var reviewed=s.Budgets.SingleOrDefault(b=>b.JobId==c.JobId)??throw new ArgumentException("Seed accepted Job budget first.");
                FinanceLedger.Required(c.Reason);
                if(c.Budget.Keys.Any(k=>!FinanceLedger.Categories.Contains(k))||c.Budget.Values.Any(v=>v.HasValue&&v<0))throw new ArgumentException("Use non-negative cost budgets or explicit unknown values.");
                foreach(var value in c.Budget.Values.Where(v=>v.HasValue))FinanceLedger.Money(value!.Value);
                var reviewedCategories=new Dictionary<string,decimal?>(reviewed.Categories);foreach(var pair in c.Budget)reviewedCategories[pair.Key]=pair.Value;
                s.BudgetReviews.Add(new(c.Id,reviewed.JobId,actor,DateTime.UtcNow,c.Reason,reviewedCategories,c.Complete));
                s.Budgets.Remove(reviewed);s.Budgets.Add(reviewed with{Categories=reviewedCategories,ActualsComplete=c.Complete,CompletenessNote=c.Reason});break;
            case "cost-completeness":
                var budget=s.Budgets.SingleOrDefault(b=>b.JobId==c.JobId)??throw new ArgumentException("Seed the Job budget first.");FinanceLedger.Required(c.Reason);
                s.Budgets.Remove(budget);s.Budgets.Add(budget with{ActualsComplete=c.Complete,CompletenessNote=c.Reason});break;
            case "job-cost":
                if(c.JobId is not Guid jid)throw new ArgumentException("Job required.");await RequireJob(jid,ct);
                if(c.Category=="MATERIAL"||!FinanceLedger.Categories.Contains(c.Category))throw new ArgumentException("Material actuals originate from vendor bills; choose a supported non-material category.");
                FinanceLedger.Required(c.SourceKey);FinanceLedger.Required(c.Reason);FinanceLedger.Positive(c.Amount);
                if(c.CostCode is not null&&!s.CostCodes.Any(code=>code.Code==c.CostCode&&code.Category==c.Category&&code.Active))throw new ArgumentException("Cost code must match the cost category.");
                if(s.Costs.Any(x=>x.SourceType=="job-cost"&&x.SourceId==c.SourceKey))throw new ArgumentException("Cost source already recorded.");
                if(c.Category=="LABOR"&&(c.Hours is null||c.Rate is null||c.Hours<=0||c.Rate<0||Math.Round(c.Hours.Value*c.Rate.Value,2,MidpointRounding.AwayFromZero)!=c.Amount))throw new ArgumentException("Labor cost must equal recorded hours times authorized rate.");
                if(c.Account==s.Operations.ApRetainageAccount||s.Construction.Accounts.Values.Contains(c.Account)||s.Policy.Accounts.Where(a=>a.Key!="equity").Any(a=>a.Value==c.Account)||!s.Accounts.Any(a=>a.Code==c.Account&&a.Type is "LIABILITY" or "EQUITY"))throw new ArgumentException("Use an accrual/equity offset for incurred costs; vendor bills handle vendor obligations.");
                var costJournal=FinanceLedger.Post(s,c.Date,"job-cost",c.SourceKey,c.Reason,[new(s.Policy.Accounts["cost"],c.Amount,0,jid,c.CostCode,Category:c.Category),new(c.Account,0,c.Amount)],actor);
                s.Costs.Add(new(Guid.NewGuid(),jid,c.Category,c.CostCode,c.Amount,c.Date,"job-cost",c.SourceKey,costJournal.Id,c.Hours,c.Rate));break;
            case "cash-account":
                var cash=c.CashAccount??throw new ArgumentException("Cash account required.");FinanceLedger.Required(cash.Id);FinanceLedger.Required(cash.Name);
                if(cash.Currency!=s.Policy.Currency||!s.Accounts.Any(a=>a.Code==cash.LedgerAccount&&a.Type=="ASSET"&&a.Active)||cash.MaskedReference.Any(char.IsDigit)&&cash.MaskedReference.Count(char.IsDigit)>4)throw new ArgumentException("Use an active asset account and at most four account-reference digits.");
                if(s.CashAccounts.Any(a=>a.Id==cash.Id||a.LedgerAccount==cash.LedgerAccount))throw new ArgumentException("Cash account or ledger account already registered.");s.CashAccounts.Add(cash);break;
            case "bank-import": ImportBank(s,c);break;
            case "bank-match": MatchBank(s,c,actor);break;
            case "reconcile": Reconcile(s,c,actor);break;
            case "opening-import": await ImportOpening(s,c,actor,ct);break;
            default:throw new ArgumentException("Unsupported Finance action.");
        }
        s.Audit.Add(new(c.Id,c.Action,c.Target,actor,DateTime.UtcNow,c.Reason,fingerprint));
        await store.SaveAsync(user.TenantId,s,expected,ct);
        return new{Saved=true};
    }
    private async Task RequireCustomer(Guid id,CancellationToken ct)
    {
        var partition=RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId);var customer=await customers.GetAsync(partition,RepositoryKeyHelper.ToRowKey(id),ct);
        if(id==Guid.Empty||customer is null||customer.IsDeleted||customer.Id!=id||customer.PartitionKey!=partition)throw new ArgumentException("Customer is unavailable in this tenant.");
    }
    private async Task RequireJob(Guid id,CancellationToken ct){if(await jobs.GetAsync(id,ct) is null)throw new ArgumentException("Job is unavailable in this tenant.");}
    private static void UpsertAccount(FinanceState s,FinanceAccount a)
    {
        FinanceLedger.Required(a.Code);FinanceLedger.Required(a.Name);
        if(!FinanceLedger.AccountTypes.Contains(a.Type)||a.NormalBalance is not ("DEBIT" or "CREDIT"))throw new ArgumentException("Invalid account classification.");
        var old=s.Accounts.SingleOrDefault(x=>x.Code==a.Code);
        if(old is not null&&(old.SystemControlled||s.Journals.Any(j=>j.Lines.Any(l=>l.Account==a.Code))))throw new ArgumentException("System/used accounts cannot be redefined.");
        if(a.SystemControlled)throw new ArgumentException("Only onboarding creates system accounts.");
        if(a.ParentCode==a.Code||a.ParentCode is not null&&!s.Accounts.Any(x=>x.Code==a.ParentCode&&x.ParentCode is null))throw new ArgumentException("Choose an existing top-level parent account.");
        s.Accounts.RemoveAll(x=>x.Code==a.Code);s.Accounts.Add(a);
    }
    private async Task SyncInvoices(FinanceState s,string actor,CancellationToken ct)
    {
        string? token=null;
        do
        {
            var page=await invoices.GetPagedAsync(100,token);token=page.ContinuationToken;
            foreach(var invoice in page.Items)
            {
                if(invoice.Status is not (InvoiceStatus.Draft or InvoiceStatus.Void))await RequireCustomer(invoice.CustomerId,ct);
                FinanceInvoicePosting.Apply(s,invoice,actor);
            }
        }while(token is not null);
    }
    private async Task SeedBudget(FinanceState s,FinanceCommand c,CancellationToken ct)
    {
        if(c.JobId is not Guid id)throw new ArgumentException("Job required.");
        var job=await jobs.GetAsync(id,ct)??throw new ArgumentException("Job not available.");
        var accepted=job.AcceptedEstimate??throw new ArgumentException("Job has no immutable accepted Estimate. Import a reconciled job budget instead.");
        var options=accepted.SelectedOptions;
        var categories=FinanceLedger.Categories.ToDictionary(k=>k,_=>(decimal?)null);
        foreach(var group in options.SelectMany(o=>o.Lines).GroupBy(l=>l.Kind.ToUpperInvariant()))
        {
            var category=group.Key switch{"MATERIAL"=>"MATERIAL","LABOR"=>"LABOR","EQUIPMENT"=>"EQUIPMENT","SUBCONTRACT"=>"SUBCONTRACT",_=>"OTHER_DIRECT_COST"};
            categories[category]=group.All(l=>l.Cost.HasValue)?group.Sum(l=>l.Cost!.Value):null;
        }
        var source=$"{accepted.EstimateId}:{accepted.Revision}:{accepted.DocumentHash}";
        if(s.Budgets.Any(b=>b.JobId==id))throw new ArgumentException("Budget already seeded. Preserve the accepted baseline; controlled budget revisions are a later extension.");
        var sold=options.Count>0?options.Sum(o=>o.Total-o.Tax):(accepted.Signature?.Total??0);
        s.Budgets.Add(new(id,source,sold,0,categories,false,"Cost data incomplete. Verify missing categories, tax basis and approved changes before relying on profitability.",TaxBasisKnown:options.Count>0));
    }
    private async Task SyncJobChanges(FinanceState s,FinanceCommand c,CancellationToken ct)
    {
        if(c.JobId is not Guid id)throw new ArgumentException("Job required.");
        var job=await jobs.GetAsync(id,ct)??throw new ArgumentException("Job unavailable.");
        var budget=s.Budgets.SingleOrDefault(b=>b.JobId==id)??throw new ArgumentException("Seed the accepted baseline first.");
        var categories=new Dictionary<string,decimal?>(budget.Categories);
        foreach(var change in job.Execution?.Changes.Where(c=>c.PricingImpact&&c.Status is "APPROVED" or "IMPLEMENTED")??[])
        {
            if(s.JobChanges.Any(v=>v.ChangeId==change.Id))continue;
            if(change.AcceptedEstimateId is not Guid estimateId||estimates is null)throw new ArgumentException("Accepted change pricing source is unavailable.");
            var packet=await estimates.GetAsync(estimateId,ct)??throw new ArgumentException("Change Estimate unavailable.");
            if(packet.CustomerId!=job.CustomerId||packet.Document?.SiteId!=job.JobSiteId)throw new ArgumentException("Change customer/site mismatch.");
            var revision=packet.RevisionNumber==change.AcceptedRevision&&packet.DocumentHash==change.AcceptedDocumentHash?new QuoteEstimateRevisionDto{Pricing=packet.Pricing,ApprovalSignature=packet.ApprovalSignature,AcceptedOptionIds=packet.AcceptedOptionIds,RevisionNumber=packet.RevisionNumber,DocumentHash=packet.DocumentHash}:packet.RevisionHistory.SingleOrDefault(r=>r.RevisionNumber==change.AcceptedRevision&&r.DocumentHash==change.AcceptedDocumentHash);
            if(revision?.ApprovalSignature is null||revision.Pricing is null)throw new ArgumentException("The exact accepted change revision has no authoritative price breakdown.");
            var selected=revision.Pricing.Options.Where(o=>o.Required||revision.AcceptedOptionIds.Contains(o.Id)).ToArray();
            if(selected.Sum(o=>o.Total)!=revision.ApprovalSignature.Total)throw new ArgumentException("Accepted change pricing does not reconcile to signed total.");
            var impacts=selected.SelectMany(o=>o.Lines).GroupBy(l=>l.Kind.ToUpperInvariant() switch{"MATERIAL"=>"MATERIAL","LABOR"=>"LABOR","EQUIPMENT"=>"EQUIPMENT","SUBCONTRACT"=>"SUBCONTRACT",_=>"OTHER_DIRECT_COST"}).ToDictionary(g=>g.Key,g=>g.All(l=>l.Cost.HasValue)?(decimal?)g.Sum(l=>l.Cost!.Value):null);
            foreach(var impact in impacts)categories[impact.Key]=categories.TryGetValue(impact.Key,out var original)&&original.HasValue&&impact.Value.HasValue?original.Value+impact.Value.Value:null;
            s.JobChanges.Add(new(id,change.Id,estimateId,revision.RevisionNumber,revision.DocumentHash,selected.Sum(o=>o.Total-o.Tax),impacts,revision.ApprovalSignature.SignedAtUtc,FinanceLedger.Hash(revision.ApprovalSignature)));
        }
        if(s.JobChanges.Where(v=>v.JobId==id).Any(v=>!job.Execution!.Changes.Any(c=>c.Id==v.ChangeId&&c.Status is "APPROVED" or "IMPLEMENTED")&&s.ContractCorrections.Where(r=>r.ChangeId==v.ChangeId).Sum(r=>r.Amount)!=-v.NetValue))throw new ArgumentException("A previously accepted financial change was cancelled. Review a controlled contract correction.");
        s.Budgets.Remove(budget);s.Budgets.Add(budget with{Categories=categories,ApprovedChanges=s.JobChanges.Where(v=>v.JobId==id).Sum(v=>v.NetValue)+s.ContractCorrections.Where(v=>v.JobId==id).Sum(v=>v.Amount),ActualsComplete=false,CompletenessNote="Approved changes refreshed from signed pricing. Review cost budget and completeness."});
    }
    private static void ImportBank(FinanceState s,FinanceCommand c)
    {
        if(c.BankTransactions.Length is <1 or >1000)throw new ArgumentException("Import between 1 and 1000 bank transactions.");
        foreach(var transaction in c.BankTransactions)
        {
            if(!s.CashAccounts.Any(a=>a.Id==transaction.CashAccountId&&a.Active))throw new ArgumentException("Cash account unavailable.");
            FinanceLedger.Required(transaction.ImportId);FinanceLedger.Required(transaction.ExternalId);FinanceLedger.Money(transaction.Amount);
            if(s.Reconciliations.Any(r=>r.CashAccountId==transaction.CashAccountId&&transaction.Date<=r.End))throw new ArgumentException("Cannot add bank activity to a reconciled interval or its opening balance.");
            if(transaction.Amount==0||transaction.Date==default)throw new ArgumentException("Bank activity requires a date and signed non-zero amount.");
            var old=s.BankTransactions.SingleOrDefault(b=>b.CashAccountId==transaction.CashAccountId&&b.ExternalId==transaction.ExternalId);
            if(old is not null){if(old.Amount!=transaction.Amount||old.Date!=transaction.Date||old.Reference!=transaction.Reference)throw new ArgumentException("External bank identity conflicts with imported content.");continue;}
            transaction.Id=Guid.NewGuid();transaction.JournalId=null;transaction.ConfirmedBy="";transaction.ReconciliationId=null;s.BankTransactions.Add(transaction);
        }
    }
    private static void MatchBank(FinanceState s,FinanceCommand c,string actor)
    {
        var bank=s.BankTransactions.SingleOrDefault(b=>b.Id.ToString()==c.Target&&b.JournalId is null&&!s.Operations.BankSplits.Any(x=>x.BankTransactionId==b.Id))??throw new ArgumentException("Unmatched bank activity not found.");
        var journal=s.Journals.SingleOrDefault(j=>j.Id.ToString()==c.Reference)??throw new ArgumentException("Journal unavailable.");
        var account=s.CashAccounts.Single(a=>a.Id==bank.CashAccountId);
        if(journal.Lines.Where(l=>l.Account==account.LedgerAccount).Sum(l=>l.Debit-l.Credit)!=bank.Amount||s.BankTransactions.Any(b=>b.CashAccountId==bank.CashAccountId&&b.JournalId==journal.Id)||s.Operations.BankSplits.Any(x=>x.JournalId==journal.Id&&s.BankTransactions.Any(b=>b.Id==x.BankTransactionId&&b.CashAccountId==bank.CashAccountId)))throw new ArgumentException("Match amount differs or journal is already matched.");
        FinanceLedger.Required(c.Reason);bank.JournalId=journal.Id;bank.ConfirmedBy=actor;
    }
    private static void Reconcile(FinanceState s,FinanceCommand c,string actor)
    {
        var account=s.CashAccounts.SingleOrDefault(a=>a.Id==c.Account&&a.Active)??throw new ArgumentException("Cash account unavailable.");
        if(c.Date==default||c.EndDate<c.Date||s.Reconciliations.Any(r=>r.CashAccountId==c.Account&&r.Start<=c.EndDate&&r.End>=c.Date))throw new ArgumentException("Reconciliation dates overlap or are invalid.");
        FinanceLedger.Money(c.Amount);FinanceLedger.Money(c.OpeningBalance);FinanceLedger.Required(c.Reference);
        var transactions=s.BankTransactions.Where(b=>b.CashAccountId==c.Account&&b.Date>=c.Date&&b.Date<=c.EndDate).ToArray();
        if(transactions.Any(b=>b.JournalId is null&&!s.Operations.BankSplits.Any(x=>x.BankTransactionId==b.Id))||c.OpeningBalance+transactions.Sum(b=>b.Amount)!=c.Amount||FinanceLedger.AccountBalance(s,account.LedgerAccount,c.EndDate)!=c.Amount||FinanceLedger.AccountBalance(s,account.LedgerAccount,c.Date.AddDays(-1))!=c.OpeningBalance)throw new ArgumentException("Unmatched activity or statement/ledger balance difference prevents reconciliation.");
        if(s.Journals.Where(j=>j.Date>=c.Date&&j.Date<=c.EndDate&&j.Lines.Any(l=>l.Account==account.LedgerAccount)).Any(j=>!transactions.Any(b=>b.JournalId==j.Id)&&s.Operations.BankSplits.Where(x=>x.JournalId==j.Id&&transactions.Any(b=>b.Id==x.BankTransactionId)).Sum(x=>x.Amount)!=j.Lines.Where(l=>l.Account==account.LedgerAccount).Sum(l=>l.Debit-l.Credit)))throw new ArgumentException("Ledger cash activity is missing from the statement match set.");
        var id=c.Id.ToString();foreach(var transaction in transactions)transaction.ReconciliationId=id;
        s.Reconciliations.Add(new(id,c.Account,c.Date,c.EndDate,c.OpeningBalance,c.Amount,actor,DateTime.UtcNow));
    }
    private async Task ImportOpening(FinanceState s,FinanceCommand c,string actor,CancellationToken ct)
    {
        if(s.Journals.Count>0||s.OpenItems.Count>0)throw new ArgumentException("Opening import is available only before the first financial posting.");
        FinanceLedger.Required(c.SourceKey);FinanceLedger.Required(c.Reason);
        if(c.ImportOpenItems.Any(i=>i.Kind is not ("AR" or "AP")||i.PartyId==Guid.Empty||i.Date>c.Date||i.DueDate<i.Date||string.IsNullOrWhiteSpace(i.ExternalId))||c.ImportOpenItems.Select(i=>i.Id).Distinct().Count()!=c.ImportOpenItems.Length||c.ImportOpenItems.Any(i=>i.Id==Guid.Empty))throw new ArgumentException("Imported open items need unique IDs, external IDs, parties and valid dates.");
        foreach(var item in c.ImportOpenItems)FinanceLedger.Positive(item.Total);
        var ar=c.ImportOpenItems.Where(i=>i.Kind=="AR").Sum(i=>i.Total);var ap=c.ImportOpenItems.Where(i=>i.Kind=="AP").Sum(i=>i.Total);
        var arGl=c.Lines.Where(l=>l.Account==s.Policy.Accounts["ar"]).Sum(l=>l.Debit-l.Credit);var apGl=c.Lines.Where(l=>l.Account==s.Policy.Accounts["ap"]).Sum(l=>l.Credit-l.Debit);
        if(ar!=c.ArControl||ap!=c.ApControl||ar!=arGl||ap!=apGl)throw new ArgumentException($"Migration control difference: AR {ar-c.ArControl}, AP {ap-c.ApControl}; GL AR {arGl-ar}, GL AP {apGl-ap}.");
        foreach(var item in c.ImportOpenItems){if(item.Kind=="AR")await RequireCustomer(item.PartyId,ct);else if(!(await supplyStore.ReadAsync(user.TenantId,ct)).Vendors.Any(v=>v.ContactId==item.PartyId))throw new ArgumentException("Imported AP vendor is unavailable in this tenant.");if(item.JobId is Guid jobId)await RequireJob(jobId,ct);}
        foreach(var line in c.Lines)if(line.JobId is Guid jobId)await RequireJob(jobId,ct);
        var journal=FinanceLedger.Post(s,c.Date,"opening-import",c.SourceKey,c.Reason,c.Lines,actor);
        s.OpenItems.AddRange(c.ImportOpenItems.Select(i=>i with{Date=c.Date,JournalId=journal.Id}));
    }
}
