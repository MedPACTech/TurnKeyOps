using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public static class PayrollEngine
{
    public static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,JobConfigurationService.Json)));
    public static void Required(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>2000)throw new ArgumentException("Provide a bounded reason or reference.");}
    public static void ValidateConfiguration(PayrollConfiguration p)
    {
        _=TimeZoneInfo.FindSystemTimeZoneById(p.Timezone);
        if(p.AiActions.Any(a=>a.Key is not("payroll.readiness" or "payroll.prepare")||a.Value is not("read" or "approval" or "auto" or "disabled")))throw new ArgumentException("Invalid Bob payroll action policy.");
        if(p.RequireJob&&!p.UsesJobs)throw new ArgumentException("Required Job allocation needs Jobs enabled.");
        if(p.Provider!="generic-csv"||p.Frequency is not("WEEKLY" or "BIWEEKLY" or "SEMIMONTHLY" or "MONTHLY")||p.LocationPolicy is not("OFF" or "CAPTURE" or "REQUIRE_WITH_OVERRIDE"))throw new ArgumentException("Unsupported provider, frequency or location policy.");
        if(p.MealBreakRequiredAfterHours is <=0 or >24||p.MealBreakMinimumMinutes is <=0 or >120)throw new ArgumentException("Invalid configured meal-break warning rule.");
        if(p.WeeklyOvertime is <=0 or >168||p.DailyOvertime is <=0 or >24||p.DailyDoubleTime is <=0 or >24||p.DailyDoubleTime<p.DailyOvertime||p.OvertimeMultiplier<1||p.DoubleTimeMultiplier<p.OvertimeMultiplier||!Enum.IsDefined(p.WeekStart))throw new ArgumentException("Invalid overtime thresholds/multipliers.");
        foreach(var codes in new[]{p.CostCodes,p.Classifications,p.EarningsTypes,p.LeaveTypes})if(codes.Length is 0 or >200||codes.Any(v=>string.IsNullOrWhiteSpace(v)||v.Length>100)||codes.Distinct().Count()!=codes.Length)throw new ArgumentException("Classification lists must be unique and nonempty.");
        if(!new[]{"REGULAR","OVERTIME","DOUBLE_TIME"}.All(p.EarningsTypes.Contains))throw new ArgumentException("Core earnings types required.");
    }
    public static decimal Duration(PayrollTimeEntry t)
    {
        if(t.ClockIn is null||t.ClockOut is null)return t.Hours;
        return Math.Round((decimal)(t.ClockOut.Value-t.ClockIn.Value).TotalHours-(t.Breaks??[]).Where(b=>!b.Paid&&b.End!=null).Sum(b=>(decimal)(b.End!.Value-b.Start).TotalHours),6);
    }
    public static PayrollProfile? Profile(PayrollState s,Guid employee,DateOnly date)=>s.Profiles.Where(p=>p.EmployeeId==employee&&p.EffectiveDate<=date).OrderByDescending(p=>p.EffectiveDate).FirstOrDefault();
    static void Open(PayrollState s,DateOnly date){if(s.Periods.Any(p=>p.Start<=date&&date<=p.End&&p.Status is not("OPEN" or "REOPENED")))throw new ArgumentException("Payroll period is locked. Use an adjustment in an open period.");}
    public static void ValidateTime(PayrollState s,PayrollTimeEntry t,Guid? excluding=null)
    {
        var p=s.Configuration;var zone=TimeZoneInfo.FindSystemTimeZoneById(t.Timezone);
        if(t.Id==Guid.Empty||t.EmployeeId==Guid.Empty||t.WorkDate==default||t.Notes.Length>2000||t.Device.Length>200||t.SourceKey.Length>300)throw new ArgumentException("Invalid time identity or evidence.");
        if(t.Source is not("CLOCK" or "MANUAL" or "CREW" or "IMPORT" or "SYSTEM" or "CORRECTION"))throw new ArgumentException("Invalid time source.");
        if(t.EarningType is "OVERTIME" or "DOUBLE_TIME"&&!(t.Source=="CORRECTION"&&t.CorrectsId!=null&&s.Time.Any(x=>x.Id==t.CorrectsId&&x.Status=="EXPORTED")))throw new ArgumentException("Overtime is calculated from configured rules, not entered as worked time.");
        if(t.JobId!=null&&!p.UsesJobs||(p.RequireJob&&t.JobId==null&&t.EarningType is ("REGULAR" or "TRAINING" or "TRAVEL"))||p.RequireCostCode&&string.IsNullOrWhiteSpace(t.CostCode)||!string.IsNullOrEmpty(t.CostCode)&&!p.CostCodes.Contains(t.CostCode)||!string.IsNullOrEmpty(t.Classification)&&!p.Classifications.Contains(t.Classification)||!p.EarningsTypes.Contains(t.EarningType))throw new ArgumentException("Invalid Job, activity, classification or earnings allocation.");
        if(t.ClockIn!=null&&DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t.ClockIn.Value,zone).DateTime)!=t.WorkDate)throw new ArgumentException("Work date must match the clock-in timezone.");
        var previousBreakEnd=t.ClockIn;foreach(var br in t.Breaks??[]){if(t.ClockIn==null||br.Start<previousBreakEnd||br.End<br.Start||br.End>t.ClockOut||br.Kind is not("PAID" or "UNPAID" or "MEAL")||br.Paid!=(br.Kind=="PAID")||br.End==null&&t.Status!="ACTIVE")throw new ArgumentException("Invalid break evidence.");previousBreakEnd=br.End??DateTimeOffset.MaxValue;}
        if(t.ClockOut!=null&&(t.ClockIn==null||t.ClockOut<=t.ClockIn))throw new ArgumentException("Clock-out must follow clock-in.");
        if(t.ClockIn!=null&&t.ClockOut!=null&&(t.ClockOut-t.ClockIn)?.TotalHours>24)throw new ArgumentException("Shifts longer than 24 hours require reviewed manual segments.");
        if(t.Status=="ACTIVE"&&s.Time.Any(x=>x.Id!=excluding&&x.EmployeeId==t.EmployeeId&&x.Status=="ACTIVE"))throw new ArgumentException("Employee already has an active clock.");
        var signedAdjustment=t.Source=="CORRECTION"&&t.CorrectsId!=null&&s.Time.Any(x=>x.Id==t.CorrectsId&&x.Status=="EXPORTED");
        if(t.Status!="ACTIVE"&&(t.Hours==0||Math.Abs(t.Hours)>24||t.Hours<0&&!signedAdjustment))throw new ArgumentException("Time must be greater than zero and at most 24 hours.");
        if(t.ClockIn!=null&&s.Time.Any(x=>x.Id!=excluding&&x.EmployeeId==t.EmployeeId&&x.Status is not("VOID" or "REJECTED" or "CORRECTED")&&x.ClockIn!=null&&x.ClockIn<(t.ClockOut??DateTimeOffset.MaxValue)&&(x.ClockOut??DateTimeOffset.MaxValue)>t.ClockIn))throw new ArgumentException("Time overlaps a recorded clock segment.");
        if(t.SourceKey!=""&&s.Time.Any(x=>x.Id!=excluding&&x.SourceKey==t.SourceKey))throw new ArgumentException("Time source already imported.");
        if(t.Location!=null&&(p.LocationPolicy=="OFF"||t.Location.Latitude is <-90 or >90||t.Location.Longitude is <-180 or >180||t.Location.Accuracy<0))throw new ArgumentException("Location evidence is disabled or invalid.");
        if(p.LocationPolicy=="REQUIRE_WITH_OVERRIDE"&&t.Source=="CLOCK"&&t.Location==null&&string.IsNullOrWhiteSpace(t.Notes))throw new ArgumentException("Capture location or provide an override explanation.");
        Open(s,t.WorkDate);
        if(t.ClockOut!=null)Open(s,DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t.ClockOut.Value.AddTicks(-1),zone).DateTime));
        if(s.Time.Where(x=>x.Id!=excluding&&x.EmployeeId==t.EmployeeId&&x.WorkDate==t.WorkDate&&x.Status is not("VOID" or "REJECTED" or "CORRECTED" or "ACTIVE")).Sum(x=>x.Hours)+t.Hours>24)throw new ArgumentException("Recorded daily time exceeds 24 hours.");
    }
    public static string[] Exceptions(PayrollState s,PayrollTimeEntry t)
    {
        var result=new List<string>();
        if(t.Status=="ACTIVE")result.Add("Missing clock-out");
        if(t.SubmittedAt!=null&&s.Periods.Any(p=>p.Start<=t.WorkDate&&p.End>=t.WorkDate&&p.Cutoff<t.SubmittedAt))result.Add("Submitted after payroll cutoff");
        if(s.Configuration.MealBreakRequiredAfterHours is decimal mealAfter&&t.Hours>mealAfter&&!(t.Breaks??[]).Any(b=>b.Kind=="MEAL"&&b.End!=null&&(decimal)(b.End.Value-b.Start).TotalMinutes>=s.Configuration.MealBreakMinimumMinutes))result.Add("Configured meal-break evidence is missing or short");
        if(t.Hours>16)result.Add("Unusual hours: review shift evidence");
        if(t.Classification=="")result.Add("Missing labor classification");
        if(Profile(s,t.EmployeeId,t.WorkDate) is not {} p||p.EmploymentStatus!="ACTIVE"||p.TerminationDate<=t.WorkDate)result.Add("Missing or inactive payroll profile");
        foreach(var wage in s.WageRequirements.Where(w=>w.JobId==t.JobId&&w.Classification==t.Classification&&w.Jurisdiction==t.Jurisdiction&&w.EffectiveFrom<=t.WorkDate&&(w.EffectiveTo==null||w.EffectiveTo>=t.WorkDate)))
        {var rate=Profile(s,t.EmployeeId,t.WorkDate)?.PayRate;if(rate==null||rate<wage.RequiredBase)result.Add("Configured prevailing base rate requires review: "+wage.Id);if(wage.RequiredFringe>0)result.Add("Configured fringe evidence requires review: "+wage.Id);}
        return result.ToArray();
    }
    public static void Apply(PayrollState s,PayrollCommand c,string actor,DateTimeOffset now)
    {
        if(c.Id==Guid.Empty)throw new ArgumentException("Command identity required.");Required(c.Reason);
        if(c.Action!="configure"&&!s.Configuration.Enabled)throw new ArgumentException("Payroll & Time is not enabled for this tenant.");
        void Event(string type,Guid id,object payload){var json=JsonSerializer.Serialize(payload,JobConfigurationService.Json);s.Events.Add(new(Guid.NewGuid(),type,id,json,Hash(json),now));}
        PayrollTimeEntry Entry()=>s.Time.SingleOrDefault(t=>t.Id==c.TargetId)??throw new ArgumentException("Time entry not found.");
        void Replace(PayrollTimeEntry t){s.Time[s.Time.FindIndex(v=>v.Id==t.Id)]=t;}
        void Add(PayrollTimeEntry input,string source,string status="DRAFT")
        {
            if(s.Time.Any(t=>t.Id==input.Id))throw new ArgumentException("Time identity exists.");
            var t=input with{Source=source,Status=status,ApprovedBy=null,ApprovedAt=null,SubmittedAt=null,SubmittedBy=null,Hours=status=="ACTIVE"?0:Duration(input)};
            if(source!="CORRECTION"&&t.CorrectsId!=null)throw new ArgumentException("Use the correction workflow.");
            ValidateTime(s,t);s.Time.Add(t);
        }
        void Unbatched(PayrollTimeEntry t){if(s.Batches.Any(b=>b.Status!="VOID"&&b.Lines.Any(l=>l.TimeId==t.Id)))throw new ArgumentException("Batched time requires an adjustment in a subsequent payroll period.");}
        switch(c.Action)
        {
            case "configure":var config=c.Configuration??throw new ArgumentException("Configuration required.");ValidateConfiguration(config);if(s.Time.Any(t=>t.Status=="ACTIVE")&&(config.Timezone!=s.Configuration.Timezone||config.Enabled!=s.Configuration.Enabled))throw new ArgumentException("Close active clocks before changing timezone or enablement.");s.Configuration=config;break;
            case "manual":if(c.Time?.ClockIn!=null||c.Time?.ClockOut!=null)throw new ArgumentException("Manual time uses duration; use import for timestamped records.");Add(c.Time??throw new ArgumentException("Time required."),"MANUAL");break;
            case "import-time":if(c.Entries.Length is 0 or >500)throw new ArgumentException("Import 1–500 records.");foreach(var t in c.Entries){Required(t.SourceKey);Add(t,"IMPORT");}break;
            case "crew-time":
                if(!s.Configuration.CrewTimeEnabled||c.Entries.Length is 0 or >100)throw new ArgumentException("Crew time disabled or empty.");
                var crew=s.Crews.SingleOrDefault(x=>x.Id==c.TargetId)??throw new ArgumentException("Crew required.");
                if(c.Entries.Select(t=>t.EmployeeId).Distinct().Count()!=c.Entries.Length||c.Entries.Any(t=>!crew.Members.Contains(t.EmployeeId)||t.WorkDate<crew.EffectiveFrom||crew.EffectiveTo<t.WorkDate))throw new ArgumentException("Crew members/dates do not match.");
                foreach(var t in c.Entries)Add(t with{CrewId=crew.Id},"CREW");break;
            case "clock-in":
                var punch=c.Time??throw new ArgumentException("Clock context required.");if(c.At==default||c.At>now.AddMinutes(5)||c.At<now.AddDays(-7))throw new ArgumentException("Clock timestamp outside sync window; use reviewed manual time.");
                Add(punch with{ClockIn=c.At,ClockOut=null,Breaks=[],WorkDate=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.At,TimeZoneInfo.FindSystemTimeZoneById(punch.Timezone)).DateTime)},"CLOCK","ACTIVE");break;
            case "clock-out":case "switch":
                var active=Entry();if(active.Status!="ACTIVE"||c.At<=active.ClockIn||c.At>now.AddMinutes(5))throw new ArgumentException("Active clock and valid punch timestamp required.");
                if((active.Breaks??[]).Any(b=>b.End==null))throw new ArgumentException("End the current break first.");
                var closed=active with{ClockOut=c.At,Status="DRAFT"};closed=closed with{Hours=Duration(closed)};ValidateTime(s,closed,active.Id);Replace(closed);
                if(c.Action=="switch"){var next=c.Time??throw new ArgumentException("New allocation required.");if(next.EmployeeId!=active.EmployeeId)throw new ArgumentException("Switch employee cannot change.");Add(next with{ClockIn=c.At,ClockOut=null,WorkDate=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(c.At,TimeZoneInfo.FindSystemTimeZoneById(next.Timezone)).DateTime),Breaks=[]},"CLOCK","ACTIVE");}break;
            case "start-break":case "end-break":
                var bt=Entry();if(bt.Status!="ACTIVE"||c.At<bt.ClockIn||c.At>now.AddMinutes(5))throw new ArgumentException("Active clock and valid break timestamp required.");Open(s,bt.WorkDate);var breaks=(bt.Breaks??[]).ToList();
                if(c.Action=="start-break"){if(breaks.Any(b=>b.End==null)||breaks.Any(b=>b.End>c.At)||c.BreakKind is not("PAID" or "UNPAID" or "MEAL"))throw new ArgumentException("Invalid break sequence.");breaks.Add(new(c.At,null,c.BreakKind,c.BreakKind=="PAID"));}
                else{if(breaks.LastOrDefault() is not {End:null} pending||c.At<=pending.Start)throw new ArgumentException("No valid pending break.");breaks[^1]=pending with{End=c.At};}Replace(bt with{Breaks=breaks.ToArray()});break;
            case "submit":var submitted=Entry();if(submitted.Status is not("DRAFT" or "REJECTED"))throw new ArgumentException("Only draft or rejected time can be submitted.");ValidateTime(s,submitted,submitted.Id);Replace(submitted with{Status=Exceptions(s,submitted).Length>0?"NEEDS_REVIEW":"SUBMITTED",SubmittedAt=now,SubmittedBy=actor});break;
            case "approve":case "reject":
                var reviewed=Entry();if(reviewed.Status is not("SUBMITTED" or "NEEDS_REVIEW"))throw new ArgumentException("Submitted time required.");ValidateTime(s,reviewed,reviewed.Id);
                if(c.Action=="approve"){if(actor==reviewed.EmployeeId.ToString("D"))throw new ArgumentException("Another authorized reviewer must approve your time.");var approved=reviewed with{Status="APPROVED",ApprovedAt=now,ApprovedBy=actor};Replace(approved);Event("PayrollTimeApproved",approved.Id,approved);}
                else Replace(reviewed with{Status="REJECTED"});break;
            case "correct":
                var original=Entry();if(original.Status is "ACTIVE" or "VOID" or "CORRECTED")throw new ArgumentException("This time cannot be corrected.");Unbatched(original);Open(s,original.WorkDate);var correction=c.Time??throw new ArgumentException("Correction required.");if(correction.EmployeeId!=original.EmployeeId)throw new ArgumentException("Correction cannot reassign employee.");Replace(original with{Status="CORRECTED"});Add(correction with{CorrectsId=original.Id},"CORRECTION");break;
            case "adjustment":
                var prior=Entry();if(!s.Batches.Any(b=>b.Status is "EXPORTED" or "PROCESSED"&&b.Lines.Any(l=>l.TimeId==prior.Id)))throw new ArgumentException("Exported original required.");
                var adj=c.Time??throw new ArgumentException("Adjustment required.");if(adj.EmployeeId!=prior.EmployeeId||adj.WorkDate<=s.Periods.Where(p=>p.Start<=prior.WorkDate&&p.End>=prior.WorkDate).Select(p=>p.End).DefaultIfEmpty(prior.WorkDate).Max()||adj.Hours==0||Math.Abs(adj.Hours)>24||adj.ClockIn!=null||adj.ClockOut!=null)throw new ArgumentException("Adjustment must use signed hours in a later open period.");
                var originalEarnings=s.Batches.Where(b=>b.Status is "EXPORTED" or "PROCESSED").SelectMany(b=>b.Lines).Where(l=>l.TimeId==prior.Id&&l.Type==adj.EarningType).ToArray();if(originalEarnings.Length==0||originalEarnings.Any(l=>l.Rate==null)||originalEarnings.Select(l=>l.Rate).Distinct().Count()!=1)throw new ArgumentException("Select an exported hourly earnings type with a single original rate. Salary adjustments require provider review.");if(adj.Hours<0&&s.Time.Where(t=>t.CorrectsId==prior.Id&&t.EarningType==adj.EarningType&&t.Status is not("REJECTED" or "VOID" or "CORRECTED")).Sum(t=>t.Hours)+adj.Hours < -originalEarnings.Sum(l=>l.Hours))throw new ArgumentException("Adjustment would reverse more hours than the original exported earnings.");
                var valid=adj with{Hours=Math.Abs(adj.Hours),CorrectsId=prior.Id,Source="CORRECTION"};ValidateTime(s,valid);s.Time.Add(adj with{Source="CORRECTION",CorrectsId=prior.Id,Status="DRAFT",ApprovedBy=null,ApprovedAt=null,SubmittedAt=null,SubmittedBy=null});break;
            case "profile":
                var profile=c.Profile??throw new ArgumentException("Payroll profile required.");if(profile.Id==Guid.Empty||profile.EmployeeId==Guid.Empty||profile.EffectiveDate==default||profile.PayType is not("HOURLY" or "SALARY")||profile.PayRate<0||profile.SalaryPerPeriod<0||profile.LaborCostRate<0||profile.PayType=="HOURLY"&&profile.PayRate==null||profile.PayType=="SALARY"&&profile.SalaryPerPeriod==null||s.Profiles.Any(p=>p.Id==profile.Id||p.EmployeeId==profile.EmployeeId&&p.EffectiveDate==profile.EffectiveDate))throw new ArgumentException("Invalid or duplicate effective payroll profile.");s.Profiles.Add(profile);break;
            case "crew":var cr=c.Crew??throw new ArgumentException("Crew required.");if(cr.Id==Guid.Empty||cr.Members.Length is 0 or >100||!cr.Members.Contains(cr.LeadId)||cr.Members.Distinct().Count()!=cr.Members.Length||cr.EffectiveTo<cr.EffectiveFrom)throw new ArgumentException("Crew requires unique members, lead and effective dates.");Required(cr.Name);s.Crews.RemoveAll(x=>x.Id==cr.Id);s.Crews.Add(cr);break;
            case "period":var period=c.Period??throw new ArgumentException("Period required.");if(period.Id==Guid.Empty||period.Start==default||period.End<period.Start||period.PayDate<period.End||period.End.DayNumber-period.Start.DayNumber>31||s.Periods.Any(p=>p.Id==period.Id||p.Start<=period.End&&p.End>=period.Start))throw new ArgumentException("Invalid or overlapping period.");s.Periods.Add(period with{Status="OPEN"});break;
            case "review-period":case "close-period":case "reopen-period":
                var pi=s.Periods.FindIndex(p=>p.Id==c.TargetId);if(pi<0)throw new ArgumentException("Period not found.");var pp=s.Periods[pi];
                if(c.Action=="close-period"&&!s.Batches.Any(b=>b.PeriodId==pp.Id&&b.Status=="PROCESSED"))throw new ArgumentException("Reconciled payroll required before closing.");
                if(c.Action=="reopen-period"&&pp.Status!="CLOSED")throw new ArgumentException("Only closed periods reopen.");
                if(c.Action=="review-period"&&pp.Status is not("OPEN" or "REOPENED"))throw new ArgumentException("Open period required.");s.Periods[pi]=pp with{Status=c.Action=="review-period"?"REVIEW":c.Action=="close-period"?"CLOSED":"REOPENED"};break;
            case "prepare":
                var payPeriod=s.Periods.SingleOrDefault(p=>p.Id==c.TargetId)??throw new ArgumentException("Period required.");if(payPeriod.Status is not("OPEN" or "REOPENED" or "REVIEW"))throw new ArgumentException("Period is locked.");
                if(s.Batches.Any(b=>b.PeriodId==payPeriod.Id&&b.Status!="VOID"))throw new ArgumentException("A prepared batch already exists for this period; use next-period adjustments.");
                if(s.Time.Any(t=>t.WorkDate>=payPeriod.Start&&t.WorkDate<=payPeriod.End&&t.Status is "ACTIVE" or "DRAFT" or "SUBMITTED" or "NEEDS_REVIEW" or "REJECTED"))throw new ArgumentException("Resolve all period time before preparing payroll.");
                var lines=Calculate(s,payPeriod);var salaryEmployees=s.Profiles.Select(p=>p.EmployeeId).Distinct().Select(id=>Profile(s,id,payPeriod.End)).Where(p=>p is {PayType:"SALARY",EmploymentStatus:"ACTIVE"}&&(p.TerminationDate==null||p.TerminationDate>=payPeriod.Start)).ToArray();if(salaryEmployees.Any(p=>!lines.Any(l=>l.EmployeeId==p!.EmployeeId)))throw new ArgumentException("Salary employee is missing approved allocation hours. Review before preparing payroll.");if(lines.Length==0)throw new ArgumentException("No approved earnings in period.");
                if(lines.Any(l=>string.IsNullOrWhiteSpace(l.ProviderEmployeeId)||string.IsNullOrWhiteSpace(l.ProviderEarningCode)||l.Gross==null))throw new ArgumentException("Provider employee/earning mappings and compensation must be configured.");
                var batch=new PayrollBatch(c.Id,payPeriod.Id,s.Batches.Count(b=>b.PeriodId==payPeriod.Id)+1,s.Configuration.Provider,lines,Hash(new{Engine="payroll-v1",s.Configuration,s.Profiles,lines}),"PREPARED",actor,now);s.Batches.Add(batch);Event("PayrollBatchPrepared",batch.Id,batch);break;
            case "void-batch":var vi=s.Batches.FindIndex(b=>b.Id==c.TargetId);if(vi<0||s.Batches[vi].Status!="PREPARED")throw new ArgumentException("Only unexported batches can be voided.");s.Batches[vi]=s.Batches[vi] with{Status="VOID"};break;
            case "export":
                var bi=s.Batches.FindIndex(b=>b.Id==c.TargetId);if(bi<0)throw new ArgumentException("Batch required.");var b=s.Batches[bi];if(b.Status!="PREPARED")throw new ArgumentException("Batch already exported.");
                var artifact=new GenericCsvPayrollProvider().PrepareExport(b);s.Batches[bi]=b with{Status="EXPORTED",Artifact=artifact,ArtifactHash=Hash(artifact)};
                foreach(var t in s.Time.Where(t=>b.Lines.Any(l=>l.TimeId==t.Id)).ToArray())Replace(t with{Status="EXPORTED"});
                var periodIndex=s.Periods.FindIndex(p=>p.Id==b.PeriodId);s.Periods[periodIndex]=s.Periods[periodIndex] with{Status="EXPORTED"};break;
            case "import-results":case "correct-result":
                var resultBatch=s.Batches.SingleOrDefault(b=>b.Id==c.TargetId)??throw new ArgumentException("Batch required.");if(resultBatch.Status is not("EXPORTED" or "PROCESSED"))throw new ArgumentException("Export payroll first.");if(c.Actuals.Length is 0 or >1000)throw new ArgumentException("Import 1–1000 actual lines.");
                foreach(var a in c.Actuals){Required(a.SourceKey);Required(a.ProviderPayrollId);if(a.Id==Guid.Empty||a.BatchId!=resultBatch.Id||!resultBatch.Lines.Any(l=>l.EmployeeId==a.EmployeeId)||a.Hours is <-168 or >168||a.EmployeeTaxes<0||a.EmployerTaxes<0||a.Deductions<0||a.NetPay<0||a.Benefits<0||a.Fees<0||a.Burden<0)throw new ArgumentException("Invalid provider result.");var old=s.Actuals.SingleOrDefault(v=>v.SourceKey==a.SourceKey);if(old!=null){if(old!=a)throw new ArgumentException("Provider source identity conflicts.");continue;}if(c.Action=="correct-result"){var corrected=s.Actuals.SingleOrDefault(v=>v.Id==a.CorrectsId&&v.BatchId==a.BatchId&&v.EmployeeId==a.EmployeeId&&v.EarningType==a.EarningType);if(corrected==null||s.Actuals.Any(v=>v.CorrectsId==corrected.Id)||resultBatch.Status=="PROCESSED")throw new ArgumentException("Correct a current unprocessed provider result with explicit lineage.");}
                    else if(a.CorrectsId!=null)throw new ArgumentException("Use reviewed provider result correction.");
                    if(resultBatch.Status=="PROCESSED"||s.Actuals.Any(v=>v.Id==a.Id)||c.Action!="correct-result"&&CurrentActuals(s).Any(v=>v.BatchId==a.BatchId&&v.EmployeeId==a.EmployeeId&&v.EarningType==a.EarningType))throw new ArgumentException("Duplicate processed employee/earnings result requires review.");s.Actuals.Add(a);Event("PayrollActualReceived",a.Id,a);}break;
            case "reconcile":
                var rb=s.Batches.FindIndex(b=>b.Id==c.TargetId);if(rb<0||s.Batches[rb].Status!="EXPORTED")throw new ArgumentException("Exported batch required.");var issues=Reconcile(s,s.Batches[rb]);if(issues.Length>0)throw new ArgumentException(string.Join("; ",issues));s.Batches[rb]=s.Batches[rb] with{Status="PROCESSED"};var pidx=s.Periods.FindIndex(p=>p.Id==s.Batches[rb].PeriodId);s.Periods[pidx]=s.Periods[pidx] with{Status="PROCESSED"};Event("PayrollBatchProcessed",s.Batches[rb].Id,new{s.Batches[rb].Id});break;
            case "leave":var leave=c.Leave??throw new ArgumentException("Leave required.");Open(s,leave.Date);if(leave.Id==Guid.Empty||leave.EmployeeId==Guid.Empty||leave.Hours is <=0 or >24||!s.Configuration.LeaveTypes.Contains(leave.Type)||s.Leave.Any(l=>l.Id==leave.Id))throw new ArgumentException("Invalid leave request.");s.Leave.Add(leave with{Status="REQUESTED",ApprovedBy=null});break;
            case "approve-leave":case "reject-leave":var li=s.Leave.FindIndex(l=>l.Id==c.TargetId);if(li<0||s.Leave[li].Status!="REQUESTED")throw new ArgumentException("Requested leave required.");Open(s,s.Leave[li].Date);if(c.Action=="approve-leave"&&actor==s.Leave[li].EmployeeId.ToString("D"))throw new ArgumentException("Another authorized reviewer must approve your leave.");
                s.Leave[li]=s.Leave[li] with{Status=c.Action=="approve-leave"?"APPROVED":"REJECTED",ApprovedBy=actor};Event("PayrollAvailabilityChanged",s.Leave[li].Id,s.Leave[li]);
                if(c.Action=="approve-leave"&&s.Leave[li].Type!="UNPAID"){var lv=s.Leave[li];var type=lv.Type=="VACATION"?"PTO":lv.Type;if(!s.Configuration.EarningsTypes.Contains(type))throw new ArgumentException("Map paid leave category to an earnings type first.");var lt=new PayrollTimeEntry(Guid.NewGuid(),lv.EmployeeId,lv.Date,s.Configuration.Timezone,null,null,lv.Hours,null,s.Configuration.CostCodes[0],Profile(s,lv.EmployeeId,lv.Date)?.Classification??s.Configuration.Classifications[0],type,"SYSTEM","SUBMITTED",lv.Reason,SourceKey:"leave:"+lv.Id,SubmittedBy:lv.EmployeeId.ToString("D"),SubmittedAt:now);ValidateTime(s,lt);s.Time.Add(lt);}break;
            case "wage-requirement":var w=c.WageRequirement??throw new ArgumentException("Wage requirement required.");Required(w.AuthorityReference);if(w.RequiredBase<0||w.RequiredFringe<0||w.JobId==Guid.Empty||!s.Configuration.UsesJobs||w.EffectiveTo<w.EffectiveFrom||s.WageRequirements.Any(x=>x.Id==w.Id))throw new ArgumentException("Invalid wage requirement.");s.WageRequirements.Add(w);break;
            default:throw new ArgumentException("Unsupported payroll action.");
        }
    }
    public static IEnumerable<PayrollActual> CurrentActuals(PayrollState s)=>s.Actuals.Where(a=>!s.Actuals.Any(next=>next.CorrectsId==a.Id));
    public static string[] Reconcile(PayrollState s,PayrollBatch batch)
    {
        var expected=batch.Lines.GroupBy(l=>(l.EmployeeId,l.Type)).ToDictionary(g=>g.Key,g=>new{Hours=g.Sum(l=>l.Hours),Gross=g.Sum(l=>l.Gross)});
        var actual=CurrentActuals(s).Where(a=>a.BatchId==batch.Id).GroupBy(a=>(a.EmployeeId,Type:a.EarningType)).ToDictionary(g=>g.Key,g=>new{Hours=g.Sum(l=>l.Hours),Gross=g.Any(l=>l.Gross==null)?null:g.Sum(l=>l.Gross)});
        var issues=new List<string>();foreach(var (key,e) in expected){if(!actual.TryGetValue(key,out var a))issues.Add($"Missing provider result: {key}");else if(e.Hours!=a.Hours||e.Gross!=a.Gross)issues.Add($"Hours/gross mismatch: {key}");}
        foreach(var key in actual.Keys.Except(expected.Keys))issues.Add($"Unexpected earnings classification: {key}");return issues.ToArray();
    }
    // Split clock segments at local midnight; UTC duration remains authoritative across DST changes.
    static IEnumerable<(DateOnly Date,decimal Hours)> Days(PayrollTimeEntry t)
    {
        if(t.ClockIn==null||t.ClockOut==null){yield return(t.WorkDate,t.Hours);yield break;}
        var zone=TimeZoneInfo.FindSystemTimeZoneById(t.Timezone);var cursor=t.ClockIn.Value;
        while(cursor<t.ClockOut.Value){var day=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cursor,zone).DateTime);var nextLocal=day.AddDays(1).ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified);var next=new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextLocal,zone));var end=next<t.ClockOut?next:t.ClockOut.Value;
            var hours=(decimal)(end-cursor).TotalHours;foreach(var br in (t.Breaks??[]).Where(b=>!b.Paid&&b.End!=null&&b.Start<end&&b.End>cursor)){var start=br.Start>cursor?br.Start:cursor;var stop=br.End!.Value<end?br.End.Value:end;hours-=(decimal)(stop-start).TotalHours;}yield return(day,Math.Round(hours,6));cursor=end;}
    }
    public static PayrollEarning[] Calculate(PayrollState s,PayrollPeriod period,bool includeCompensation=true)
    {
        var result=new List<PayrollEarning>();var policy=s.Configuration;
        var slices=s.Time.Where(t=>t.Status is "APPROVED" or "EXPORTED").SelectMany(t=>Days(t).Select(d=>new{Time=t,d.Date,d.Hours})).OrderBy(v=>v.Date).ThenBy(v=>v.Time.ClockIn).ThenBy(v=>v.Time.Id).ToArray();
        var daily=new Dictionary<(Guid,DateOnly),decimal>();var weekly=new Dictionary<(Guid,DateOnly),decimal>();
        foreach(var slice in slices)
        {
            var t=slice.Time;var date=slice.Date;var profile=Profile(s,t.EmployeeId,date);var week=date.AddDays(-((7+(int)date.DayOfWeek-(int)policy.WeekStart)%7));var dk=(t.EmployeeId,date);var wk=(t.EmployeeId,week);var dayHours=daily.GetValueOrDefault(dk);var weekRegular=weekly.GetValueOrDefault(wk);
            if(t.CorrectsId!=null&&s.Time.Any(x=>x.Id==t.CorrectsId&&x.Status=="EXPORTED")){if(date>=period.Start&&date<=period.End){var basis=s.Batches.Where(b=>b.Status is "EXPORTED" or "PROCESSED").SelectMany(b=>b.Lines).First(l=>l.TimeId==t.CorrectsId&&l.Type==t.EarningType);result.Add(basis with{TimeId=t.Id,Date=date,Hours=slice.Hours,Gross=basis.Rate*slice.Hours,JobId=t.JobId,CostCode=t.CostCode,Classification=t.Classification});}continue;}
            var worked=t.EarningType is "REGULAR" or "TRAINING" or "TRAVEL";var eligible=profile?.OvertimeEligible==true&&worked&&!(t.CorrectsId!=null&&s.Time.Any(x=>x.Id==t.CorrectsId&&x.Status=="EXPORTED"));
            var dt=eligible&&policy.DailyDoubleTime is decimal dtAt?Math.Max(0,dayHours+slice.Hours-Math.Max(dayHours,dtAt)):0;
            var dailyOt=eligible&&policy.DailyOvertime is decimal otAt?Math.Max(0,dayHours+slice.Hours-Math.Max(dayHours,otAt))-dt:0;
            if(eligible&&policy.SeventhDayOvertime&&Enumerable.Range(1,6).All(i=>slices.Any(v=>v.Time.EmployeeId==t.EmployeeId&&v.Date==date.AddDays(-i)&&v.Hours>0)))dailyOt=slice.Hours-dt;
            var regular=slice.Hours-dt-dailyOt;var weeklyOt=eligible&&policy.WeeklyOvertime is decimal weekAt?Math.Max(0,weekRegular+regular-Math.Max(weekRegular,weekAt)):0;regular-=weeklyOt;
            if(worked){daily[dk]=dayHours+slice.Hours;weekly[wk]=weekRegular+regular;}
            void Line(string type,decimal hours,decimal multiplier){if(hours==0||date<period.Start||date>period.End)return;decimal? rate=profile?.PayType=="HOURLY"?profile.PayRate*multiplier:null;decimal? gross=rate*hours;result.Add(new(t.Id,t.EmployeeId,date,t.JobId,t.CostCode,t.Classification,type,hours,rate,gross,profile?.ProviderEmployeeId??"",policy.EarningsMapping.GetValueOrDefault(type,""),t.Trade,t.Jurisdiction,profile?.LaborCostRate));}
            Line(t.EarningType,regular,1);Line("OVERTIME",dailyOt+weeklyOt,policy.OvertimeMultiplier);Line("DOUBLE_TIME",dt,policy.DoubleTimeMultiplier);
        }
        foreach(var group in result.Where(l=>includeCompensation).Where(l=>Profile(s,l.EmployeeId,l.Date)?.PayType=="SALARY").GroupBy(l=>l.EmployeeId).ToArray()){
            var p=Profile(s,group.Key,period.End)!;if(p.OvertimeEligible)throw new ArgumentException("Non-exempt salary needs a configured provider calculation; automatic salary overtime is unsupported.");var total=group.Sum(l=>l.Hours);if(total<=0)throw new ArgumentException("Salary allocation needs positive approved hours.");decimal used=0;var items=group.ToArray();for(var i=0;i<items.Length;i++){var gross=i==items.Length-1?p.SalaryPerPeriod-used:Math.Round((p.SalaryPerPeriod??0)*items[i].Hours/total,2);used+=gross??0;var ix=result.IndexOf(items[i]);result[ix]=items[i] with{Gross=gross};}}
        return result.Select(l=>l with{Gross=l.Gross==null?null:Math.Round(l.Gross.Value,2)}).ToArray();
    }
}
public interface IPayrollProvider
{
    string Id { get; }
    string PrepareExport(PayrollBatch batch);
    string[] Reconcile(PayrollState state,PayrollBatch batch);
}
public sealed class GenericCsvPayrollProvider:IPayrollProvider
{
    public string Id=>"generic-csv";
    public string PrepareExport(PayrollBatch batch)
    {
        static string Cell(object? v){var s=Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture)??"";var index=0;while(index<s.Length&&(char.IsWhiteSpace(s[index])||char.IsControl(s[index])||s[index]=='\uFEFF'))index++;if(v is string&&index<s.Length&&s[index] is '=' or '+' or '-' or '@')s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
        var rows=new List<object?[]>{new object?[]{"Batch","Version","Time","Employee","ProviderEmployee","Date","EarningCode","EarningType","Hours","Rate","Gross","Job","Activity","Classification","Trade","Jurisdiction"}};
        rows.AddRange(batch.Lines.Select(l=>new object?[]{batch.Id,batch.ExportVersion,l.TimeId,l.EmployeeId,l.ProviderEmployeeId,l.Date.ToString("yyyy-MM-dd"),l.ProviderEarningCode,l.Type,l.Hours,l.Rate,l.Gross,l.JobId,l.CostCode,l.Classification,l.Trade,l.Jurisdiction}));return string.Join("\r\n",rows.Select(r=>string.Join(',',r.Select(Cell))));
    }
    public string[] Reconcile(PayrollState state,PayrollBatch batch)=>PayrollEngine.Reconcile(state,batch);
}
