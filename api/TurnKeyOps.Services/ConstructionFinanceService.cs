using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed class ConstructionFinanceService(IFinanceStore store,IFinanceAuthority authority,IUserContext user,ISupplyStore supply,IJobService jobs,IFinanceSourceCoordinator? coordinator=null)
{
    public async Task<object> WorkspaceAsync(DateOnly? asOf=null,CancellationToken ct=default)
    {
        await authority.RequireAsync(false,ct);var s=await store.ReadAsync(user.TenantId,ct);var p=await supply.ReadAsync(user.TenantId,ct);
        var end=asOf??DateOnly.FromDateTime(DateTime.UtcNow);
        var snapshot=s.Construction.WipSnapshots.SingleOrDefault(w=>w.Status=="POSTED"&&s.Periods.Any(period=>period.Id==w.PeriodId&&period.End==end));
        var results=snapshot?.Jobs??s.Construction.Contracts.Select(c=>ConstructionFinanceEngine.Calculate(s,p,c,end)).ToArray();
        var sov=s.Construction.Contracts.SelectMany(contract=>contract.Lines.Select(line=>{
            var billed=s.Construction.Applications.Where(a=>a.JobId==contract.JobId&&a.IsPosted&&a.PeriodEnd<=end).SelectMany(a=>a.Lines).Where(l=>l.SovLineId==line.Id).Sum(l=>l.BillableAmount)+(s.Construction.OpeningBalances.SingleOrDefault(b=>b.JobId==contract.JobId&&b.Date<=end)?.Lines.Where(l=>l.SovLineId==line.Id).Sum(l=>l.BillableAmount)??0);
            var latest=s.Construction.Applications.Where(a=>a.JobId==contract.JobId&&a.IsPosted&&a.PeriodEnd<=end).OrderByDescending(a=>a.PeriodEnd).FirstOrDefault();var current=latest?.Lines.Where(l=>l.SovLineId==line.Id).Sum(l=>l.BillableAmount)??0;
            var history=s.Construction.Applications.Where(a=>a.JobId==contract.JobId&&a.IsPosted&&a.PeriodEnd<=end).SelectMany(a=>a.Lines).Where(l=>l.SovLineId==line.Id).Concat(s.Construction.OpeningBalances.Where(b=>b.JobId==contract.JobId&&b.Date<=end).SelectMany(b=>b.Lines).Where(l=>l.SovLineId==line.Id)).ToArray();
            return new{contract.JobId,line.Id,line.Description,line.CostCode,line.OriginalValue,line.ApprovedChanges,line.CurrentValue,PriorBilled=billed-current,CurrentBilled=current,TotalBilled=billed,CompletedWorkToDate=history.Sum(l=>l.CompletedWorkThisPeriod),StoredMaterialsToDate=history.Sum(l=>l.NewStoredMaterials-l.InstalledFromStored),NewStoredMaterials=latest?.Lines.Where(l=>l.SovLineId==line.Id).Sum(l=>l.NewStoredMaterials)??0,InstalledFromStored=latest?.Lines.Where(l=>l.SovLineId==line.Id).Sum(l=>l.InstalledFromStored)??0,PercentComplete=line.CurrentValue==0?0:billed/line.CurrentValue,RetainageWithheldToDate=ConstructionFinanceEngine.SovRetainage(s,contract.JobId,line.Id,end),BalanceToFinish=line.CurrentValue-billed};})).ToArray();
        var costDrilldown=s.Costs.Where(c=>c.Date<=end).GroupBy(c=>new{c.JobId,c.Category,c.CostCode}).Select(g=>new{g.Key.JobId,g.Key.Category,g.Key.CostCode,Actual=g.Sum(c=>c.Amount),Sources=g.Select(c=>new{c.Id,c.SourceType,c.SourceId,c.JournalId,c.Date,c.Amount}).ToArray()}).ToArray();
        var tradeByJob=new Dictionary<Guid,string>();foreach(var con in s.Construction.Contracts)tradeByJob[con.JobId]=(await jobs.GetAsync(con.JobId,ct))?.Execution?.TradeProfile??"general";
        var categoryReport=s.Construction.Contracts.SelectMany(con=>FinanceLedger.Categories.Append("UNCLASSIFIED").Select(category=>{
            var actual=s.Costs.Where(v=>v.JobId==con.JobId&&v.Category==category&&v.Date<=end).Sum(v=>v.Amount);
            var budget=s.Budgets.SingleOrDefault(v=>v.JobId==con.JobId)?.Categories.GetValueOrDefault(category);
            var forecast=s.Construction.Forecasts.Where(v=>v.JobId==con.JobId&&v.Date<=end).OrderByDescending(v=>v.Date).ThenByDescending(v=>v.AtUtc).FirstOrDefault();
            decimal? remaining=forecast?.Complete==true&&forecast.Costs!=null&&(forecast.BasisHash.Length==0||forecast.BasisHash==ConstructionFinanceEngine.ForecastBasis(s,con.JobId))?forecast.Costs.Where(v=>v.Category==category).Sum(v=>v.RemainingForecast):null;
            var committed=FinanceInsights.Commitments(s,p,con.JobId,end).Where(v=>ConstructionFinanceEngine.CommitmentCategory(s,p,con.JobId,v,end)==category).Sum(v=>v.Amount);
            foreach(var imported in s.ImportedCommitments.Where(v=>v.JobId==con.JobId&&v.Category==category&&v.Date<=end&&v.SupplyOrderId==null))committed+=Math.Max(0,imported.RemainingAmount-s.Bills.Where(b=>b.JournalId!=null&&b.Date<=end).SelectMany(b=>b.Lines).Where(l=>l.ImportedCommitmentId==imported.Id).Sum(l=>l.Amount));
            var codeResults=ConstructionCodeBudgets.Report(s,p,con.JobId,end).Where(r=>r.Category==category).ToArray();
            decimal? eac=remaining==null?null:codeResults.Any(r=>r.EstimatedFinalCost==null)?actual+Math.Max(committed,remaining.Value):codeResults.Sum(r=>r.EstimatedFinalCost!.Value);
            return new{con.JobId,Trade=tradeByJob[con.JobId],Category=category,Budget=budget,RemainingCommitment=committed,Actual=actual,ForecastRemaining=remaining,EstimatedFinalCost=eac,ActualVariance=budget-actual,ForecastVariance=budget-eac,ForecastComplete=remaining!=null};})).ToArray();
        return new{Version=s.Version,State=s.Construction,Jobs=results,SnapshotId=snapshot?.Id,ScheduleOfValues=sov,CostDrilldown=costDrilldown,CodeReport=s.Construction.Contracts.SelectMany(c=>ConstructionCodeBudgets.Report(s,p,c.JobId,end)).ToArray(),CategoryReport=categoryReport,TradeByJob=tradeByJob,CanWrite=await authority.CanWriteAsync(ct),CanControl=await authority.CanControlAsync(ct)};
    }
    public async Task<object> CommandAsync(ConstructionFinanceCommand c,CancellationToken ct=default)
    {
        await authority.RequireAsync(true,ct);if(c.Id==Guid.Empty)throw new ArgumentException("Command identity required.");
        if(c.Action is not ("application" or "submit-application" or "calculate-wip" or "draft-code-budget")&&!await authority.CanControlAsync(ct))throw new MedInsights.Lib.ForbiddenAccessException("An authorized Finance owner must perform this control action.");
        await using var sourceLease=coordinator is not null&&c.Action=="contract"?await coordinator.AcquireAsync(user.TenantId,ct):null;
        var s=await store.ReadAsync(user.TenantId,ct);var fingerprint=FinanceLedger.Hash(withVersion());
        object withVersion()=>new{c.Id,c.Action,c.JobId,c.Target,c.PeriodId,c.Reason,c.Date,c.Amount,c.Contract,c.Application,c.Forecast,c.Accounts,c.CodeBudget,c.CommitmentCoding};
        var old=s.Audit.SingleOrDefault(a=>a.CommandId==c.Id);if(old!=null){if(old.Fingerprint!=fingerprint)throw new ArgumentException("Command identity conflict.");return new{Version=s.Version,Replayed=true};}
        if(s.Version!=c.ExpectedVersion)throw new InvalidOperationException("Finance changed.");
        var actor=user.UserId.ToString();var state=s.Construction;string? observedSupplyVersion=null;
        if(c.JobId!=Guid.Empty&&await jobs.GetAsync(c.JobId,ct) is null)throw new ArgumentException("Job unavailable in this tenant.");
        string Account(string role)=>state.Accounts.TryGetValue(role,out var value)?value:throw new ArgumentException("Configure construction posting accounts first.");
        void Open(DateOnly date){if(date==default||!s.Periods.Any(p=>p.Status=="OPEN"&&date>=p.Start&&date<=p.End))throw new ArgumentException("An open accounting period is required.");}
        switch(c.Action)
        {
            case "draft-code-budget":
                Open(c.Date);FinanceLedger.Required(c.Reason);var allocation=c.CodeBudget??throw new ArgumentException("Cost-code budget required.");
                if(allocation.JobId!=c.JobId)throw new ArgumentException("Job mismatch.");ConstructionCodeBudgets.Validate(s,allocation);
                state.CodeBudgets.Add(allocation with{Id=c.Id,Date=c.Date,Status="DRAFT",Reason=c.Reason,Actor=actor,AtUtc=DateTime.UtcNow,CategoryBasisHash=ConstructionCodeBudgets.CategoryBasis(s,c.JobId),CategorySnapshot=new(s.Budgets.Single(b=>b.JobId==c.JobId).Categories)});break;
            case "review-code-budget":
                var review=state.CodeBudgets.SingleOrDefault(b=>b.Id==c.Target&&b.Status=="DRAFT")??throw new ArgumentException("Draft cost-code budget unavailable.");Open(review.Date);
                if(state.WipSnapshots.Any(w=>w.Status=="POSTED"&&w.Jobs.Any(j=>j.JobId==review.JobId)&&s.Periods.Any(p=>p.Id==w.PeriodId&&review.Date<=p.End)))throw new ArgumentException("Reviewed allocations cannot change locked WIP periods.");
                if(review.CategoryBasisHash!=ConstructionCodeBudgets.CategoryBasis(s,review.JobId))throw new ArgumentException("Category budget changed; create a refreshed allocation draft.");
                if(state.CodeBudgets.Any(b=>b.JobId==review.JobId&&b.Status=="REVIEWED"&&b.Date>review.Date))throw new ArgumentException("Review budget revisions chronologically.");
                ConstructionCodeBudgets.Validate(s,review);state.CodeBudgets.RemoveAll(b=>b.Id==review.Id);state.CodeBudgets.Add(review with{Status="REVIEWED",Actor=actor,AtUtc=DateTime.UtcNow});break;
            case "code-commitment":
                Open(c.Date);FinanceLedger.Required(c.Reason);var codeInput=c.CommitmentCoding??throw new ArgumentException("Commitment coding required.");if(codeInput.JobId!=c.JobId)throw new ArgumentException("Job mismatch.");
                var codingSupply=await supply.ReadAsync(user.TenantId,ct);observedSupplyVersion=codingSupply.Version;
                string sourceCategory;
                if(codeInput.ImportedCommitmentId is not null){var imported=s.ImportedCommitments.SingleOrDefault(v=>v.Id==codeInput.ImportedCommitmentId&&v.JobId==c.JobId&&v.SupplyOrderId==null&&v.Date<=c.Date)??throw new ArgumentException("Imported Job commitment unavailable.");if(codeInput.OrderId!=Guid.Empty||codeInput.OrderLineId!=Guid.Empty)throw new ArgumentException("Use one commitment source identity.");sourceCategory=imported.Category;}
                else{var source=FinanceInsights.Commitments(s,codingSupply,c.JobId,c.Date).SingleOrDefault(v=>v.OrderId==codeInput.OrderId&&v.OrderLineId==codeInput.OrderLineId)??throw new ArgumentException("Canonical Job commitment unavailable.");sourceCategory=ConstructionFinanceEngine.CommitmentCategory(s,codingSupply,c.JobId,source,c.Date);}
                var costCode=s.CostCodes.SingleOrDefault(v=>v.Code==codeInput.CostCode&&v.Active)??throw new ArgumentException("Active cost code required.");if(sourceCategory!="UNCLASSIFIED"&&sourceCategory!=costCode.Category)throw new ArgumentException("Commitment category must match the canonical source.");
                if(state.WipSnapshots.Any(w=>w.Status=="POSTED"&&w.Jobs.Any(j=>j.JobId==c.JobId)&&s.Periods.Any(p=>p.Id==w.PeriodId&&c.Date<=p.End)))throw new ArgumentException("Commitment coding cannot change locked WIP inputs.");
                state.CommitmentCodings.Add(codeInput with{Id=c.Id,Date=c.Date,Actor=actor,AtUtc=DateTime.UtcNow});break;
            case "policy":
                if(state.OpeningBalances.Count>0||s.Journals.Any(j=>j.Lines.Any(l=>state.Accounts.Values.Contains(l.Account)))||state.Applications.Any(a=>a.IsPosted)||state.WipSnapshots.Any(w=>w.Status=="POSTED"))throw new ArgumentException("Used construction mappings are immutable.");
                var mappings=c.Accounts??throw new ArgumentException("Mappings required.");if(mappings.Count!=4)throw new ArgumentException("Exactly four construction posting roles are required.");
                foreach(var pair in new Dictionary<string,string>{{"retainage","ASSET"},{"contractAsset","ASSET"},{"contractLiability","LIABILITY"},{"billing","LIABILITY"}})if(!mappings.TryGetValue(pair.Key,out var code)||!s.Accounts.Any(a=>a.Code==code&&a.Active&&a.Type==pair.Value))throw new ArgumentException("Map each construction role to an active account of the correct type.");
                if(mappings.Values.Distinct().Count()!=mappings.Count||mappings.Values.Intersect(s.Policy.Accounts.Values).Any()||mappings.Values.Contains(s.Operations.ApRetainageAccount))throw new ArgumentException("Construction control accounts must be distinct.");state.Accounts=new(mappings);break;
            case "contract":
                var contract=c.Contract??throw new ArgumentException("Contract required.");if(contract.JobId!=c.JobId||(await jobs.GetAsync(c.JobId,ct))?.CustomerId!=contract.CustomerId)throw new ArgumentException("Job/customer mismatch.");ConstructionFinanceEngine.ValidateContract(s,contract);
                if(!state.Contracts.Any(v=>v.JobId==c.JobId)&&s.OpenItems.Any(i=>i.JobId==c.JobId&&i.Kind=="AR"))throw new ArgumentException("Existing conventional billing requires a verified construction migration before opt-in.");
                var previous=state.Contracts.SingleOrDefault(v=>v.JobId==c.JobId);
                if(previous!=null&&(state.OpeningBalances.Any(b=>b.JobId==c.JobId)||state.Applications.Any(a=>a.JobId==c.JobId&&a.IsPosted)||state.WipSnapshots.Any(w=>w.Status=="POSTED"&&w.Jobs.Any(j=>j.JobId==c.JobId))))
                {
                    if(previous.CustomerId!=contract.CustomerId||previous.OriginalValue!=contract.OriginalValue||previous.RecognitionMethod!=contract.RecognitionMethod||previous.RetainagePercent!=contract.RetainagePercent)throw new ArgumentException("Used contract identity and recognition policy are immutable.");
                    foreach(var line in previous.Lines)if(!contract.Lines.Any(l=>l.Id==line.Id&&l.OriginalValue==line.OriginalValue))throw new ArgumentException("Used SOV original values are immutable.");
                    foreach(var line in contract.Lines)if((state.OpeningBalances.SingleOrDefault(b=>b.JobId==c.JobId)?.Lines.Where(l=>l.SovLineId==line.Id).Sum(l=>l.BillableAmount)??0)+state.Applications.Where(a=>a.JobId==c.JobId&&a.IsPosted).SelectMany(a=>a.Lines).Where(l=>l.SovLineId==line.Id).Sum(l=>l.BillableAmount)>line.CurrentValue)throw new ArgumentException("Revised SOV cannot fall below billing.");
                }
                state.Contracts.RemoveAll(v=>v.JobId==c.JobId);state.Contracts.Add(contract);state.ContractRevisions.Add(new(c.Id,contract,actor,DateTime.UtcNow,c.Reason,s.JobChanges.Where(v=>v.JobId==c.JobId).Select(v=>v.ChangeId).ToArray()));break;
            case "forecast":
                Open(c.Date);FinanceLedger.Required(c.Reason);var f=c.Forecast??throw new ArgumentException("Forecast required.");if(f.JobId!=c.JobId||f.RemainingForecast<0)throw new ArgumentException("Invalid forecast.");FinanceLedger.Money(f.RemainingForecast);if(f.Costs!=null){if(f.Costs.Sum(v=>v.RemainingForecast)!=f.RemainingForecast||f.Costs.Select(v=>(v.Category,v.CostCode)).Distinct().Count()!=f.Costs.Length)throw new ArgumentException("Forecast cost breakdown must reconcile to remaining forecast.");foreach(var fc in f.Costs){FinanceLedger.Money(fc.RemainingForecast);if(fc.RemainingForecast<0||!FinanceLedger.Categories.Contains(fc.Category)||fc.CostCode!=null&&!s.CostCodes.Any(code=>code.Code==fc.CostCode&&code.Active&&code.Category==fc.Category))throw new ArgumentException("Invalid forecast cost coding.");}}state.Forecasts.Add(f with{Id=c.Id,Date=c.Date,Reason=c.Reason,Actor=actor,AtUtc=DateTime.UtcNow,BasisHash=ConstructionFinanceEngine.ForecastBasis(s,c.JobId)});break;
            case "application":
                var application=c.Application??throw new ArgumentException("Application required.");if(application.JobId!=c.JobId)throw new ArgumentException("Job mismatch.");
                if(state.Applications.Any(a=>a.JobId==c.JobId&&a.Id!=application.Id&&!a.IsPosted&&a.Status!="VOID"))throw new ArgumentException("Complete or void the existing active application before creating another.");
                if(state.Applications.Any(a=>a.Id==application.Id&&a.Status!="DRAFT"))throw new ArgumentException("Submitted applications cannot be edited.");
                if(state.Applications.Any(a=>a.Id!=application.Id&&a.JobId==c.JobId&&a.Number==application.Number))throw new ArgumentException("Duplicate application number.");
                application=ConstructionFinanceEngine.ValidateApplication(s,application with{Status="DRAFT",JournalId=null,InvoiceId=null});state.Applications.RemoveAll(a=>a.Id==application.Id);state.Applications.Add(application);break;
            case "void-application":
                var voidApp=state.Applications.SingleOrDefault(a=>a.Id==c.Target&&!a.IsPosted&&a.Status!="VOID")??throw new ArgumentException("Unposted application unavailable.");FinanceLedger.Required(c.Reason);state.Applications.RemoveAll(a=>a.Id==voidApp.Id);state.Applications.Add(voidApp with{Status="VOID"});break;
            case "submit-application":case "approve-application":case "invoice-application":
                var app=state.Applications.SingleOrDefault(a=>a.Id==c.Target)??throw new ArgumentException("Application unavailable.");
                var required=c.Action=="submit-application"?"DRAFT":c.Action=="approve-application"?"IN_REVIEW":"APPROVED";if(app.Status!=required)throw new ArgumentException("Invalid application transition.");
                app=ConstructionFinanceEngine.ValidateApplication(s,app);
                if(c.Action=="invoice-application")
                {
                    var con=state.Contracts.Single(v=>v.JobId==app.JobId);var lines=new List<FinanceLine>();
                    if(app.TotalDue>0)lines.Add(new(s.Policy.Accounts["ar"],app.TotalDue,0,app.JobId,CustomerId:con.CustomerId));
                    if(app.Retainage>0)lines.Add(new(Account("retainage"),app.Retainage,0,app.JobId,CustomerId:con.CustomerId));
                    lines.Add(new(Account("billing"),0,app.CurrentBilling,app.JobId));
                    Open(app.PeriodEnd);if(state.WipSnapshots.Any(w=>w.Status=="POSTED"&&w.Jobs.Any(j=>j.JobId==app.JobId)&&s.Periods.Any(p=>p.Id==w.PeriodId&&app.PeriodEnd<=p.End)))throw new ArgumentException("Posted WIP locks application inputs. Use the next open period.");
                    var journal=app.CurrentBilling>0?FinanceLedger.Post(s,app.PeriodEnd,"construction-billing",app.Id.ToString(),"Progress application "+app.Number,lines.ToArray(),actor):null;
                    if(app.TotalDue>0)s.OpenItems.Add(new(app.Id,"AR",con.CustomerId,app.Number,app.PeriodEnd,app.PeriodEnd.AddDays(30),app.TotalDue,journal!.Id,app.JobId));
                    app=app with{Status="INVOICED",JournalId=journal?.Id,InvoiceId=app.TotalDue>0?app.Id:null};
                }else app=app with{Status=c.Action=="submit-application"?"IN_REVIEW":"APPROVED"};
                state.Applications.RemoveAll(a=>a.Id==app.Id);state.Applications.Add(app);break;
            case "release-retainage":
                FinanceLedger.Positive(c.Amount);FinanceLedger.Required(c.Reason);var releaseContract=state.Contracts.Single(v=>v.JobId==c.JobId);
                var available=(state.OpeningBalances.SingleOrDefault(b=>b.JobId==c.JobId&&b.Date<=c.Date)?.Retainage??0)+state.Applications.Where(a=>a.JobId==c.JobId&&a.IsPosted&&a.PeriodEnd<=c.Date).Sum(a=>a.Retainage)-state.RetainageReleases.Where(r=>r.JobId==c.JobId).Sum(r=>r.Amount);
                if(c.Amount>available)throw new ArgumentException("Release exceeds held retainage.");
                var release=FinanceLedger.Post(s,c.Date,"retainage-release",c.Id.ToString(),c.Reason,[new(s.Policy.Accounts["ar"],c.Amount,0,c.JobId,CustomerId:releaseContract.CustomerId),new(Account("retainage"),0,c.Amount,c.JobId)],actor);
                s.OpenItems.Add(new(c.Id,"AR",releaseContract.CustomerId,"RET-"+c.Id.ToString("N"),c.Date,c.Date.AddDays(30),c.Amount,release.Id,c.JobId));state.RetainageReleases.Add(new(c.Id,c.JobId,c.Date,c.Amount,release.Id,c.Id,c.Reason));break;
            case "calculate-wip":
                var period=s.Periods.SingleOrDefault(p=>p.Id==c.PeriodId&&p.Status=="OPEN")??throw new ArgumentException("Open period required.");
                if(state.WipSnapshots.Any(w=>w.PeriodId==period.Id))throw new ArgumentException("Period snapshot already exists.");
                var supplyState=await supply.ReadAsync(user.TenantId,ct);var results=state.Contracts.Select(v=>ConstructionFinanceEngine.Calculate(s,supplyState,v,period.End)).ToArray();
                if(results.Length==0)throw new ArgumentException("Configure construction Jobs first.");var payload=ConstructionFinanceEngine.SourcePayload(s,supplyState,period.End);state.WipSnapshots.Add(new(c.Id,period.Id,results,"DRAFT",actor,DateTime.UtcNow,SourcePayloadJson:payload,SourceHash:FinanceLedger.Hash(payload)));break;
            case "approve-wip":case "post-wip":
                var wip=state.WipSnapshots.SingleOrDefault(w=>w.Id==c.Target)??throw new ArgumentException("Snapshot unavailable.");
                if(wip.Status!=(c.Action=="approve-wip"?"DRAFT":"APPROVED")||wip.Jobs.Any(j=>j.Exceptions.Length>0))throw new ArgumentException("Review WIP exceptions and lifecycle first.");
                var wperiod=s.Periods.Single(p=>p.Id==wip.PeriodId);Open(wperiod.End);
                // Approval and posting refuse a stale source snapshot.
                var currentSupply=await supply.ReadAsync(user.TenantId,ct);observedSupplyVersion=currentSupply.Version;var current=state.Contracts.Select(v=>ConstructionFinanceEngine.Calculate(s,currentSupply,v,wperiod.End)).ToArray();
                if(FinanceLedger.Hash(current)!=FinanceLedger.Hash(wip.Jobs)||FinanceLedger.Hash(ConstructionFinanceEngine.SourcePayload(s,currentSupply,wperiod.End))!=wip.SourceHash)throw new ArgumentException("WIP inputs changed; discard draft and calculate again.");
                if(c.Action=="post-wip")
                {
                    if(state.WipSnapshots.Any(w=>w.Status=="POSTED"&&s.Periods.Single(p=>p.Id==w.PeriodId).End>wperiod.End))throw new ArgumentException("Post WIP periods chronologically.");
                    var wlines=new List<FinanceLine>();
                    foreach(var job in wip.Jobs)
                    {
                        decimal Balance(string account)=>s.Journals.Where(j=>j.Date<=wperiod.End).SelectMany(j=>j.Lines).Where(l=>l.JobId==job.JobId&&l.Account==account).Sum(l=>l.Debit-l.Credit);
                        void Target(string account,decimal target){var delta=target-Balance(account);if(delta!=0)wlines.Add(new(account,Math.Max(0,delta),Math.Max(0,-delta),job.JobId));}
                        Target(Account("billing"),0);Target(Account("contractAsset"),job.Underbilling!.Value);Target(Account("contractLiability"),-job.Overbilling!.Value);
                        Target(s.Policy.Accounts["revenue"],-(job.EarnedRevenue!.Value-(state.OpeningBalances.SingleOrDefault(b=>b.JobId==job.JobId)?.EarnedRevenue??0)));
                    }
                    Guid? jid=null;if(wlines.Count>0)jid=FinanceLedger.Post(s,wperiod.End,"construction-wip",wip.Id.ToString(),"WIP "+wip.PeriodId,wlines.ToArray(),actor).Id;
                    wip=wip with{Status="POSTED",JournalId=jid,Jobs=wip.Jobs.Select(j=>j with{RecognizedToDate=j.EarnedRevenue??j.RecognizedToDate}).ToArray()};
                }else wip=wip with{Status="APPROVED"};state.WipSnapshots.RemoveAll(w=>w.Id==wip.Id);state.WipSnapshots.Add(wip);break;
            case "discard-wip":
                if(!state.WipSnapshots.RemoveAll(w=>w.Id==c.Target&&w.Status!="POSTED").Equals(1))throw new ArgumentException("Unposted snapshot unavailable.");break;
            default:throw new ArgumentException("Unknown construction action.");
        }
        if(observedSupplyVersion!=null&&(await supply.ReadAsync(user.TenantId,ct)).Version!=observedSupplyVersion)throw new InvalidOperationException("Supply commitments changed during WIP posting.");
        s.Audit.Add(new(c.Id,"construction."+c.Action,c.Target.ToString(),actor,DateTime.UtcNow,c.Reason,fingerprint));await store.SaveAsync(user.TenantId,s,c.ExpectedVersion,ct);return new{Version=(await store.ReadAsync(user.TenantId,ct)).Version};
    }
}
