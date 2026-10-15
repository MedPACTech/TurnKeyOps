using System.Text.Json;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;

public sealed class PayrollTests
{
    static readonly Guid Employee=Guid.Parse("11111111-1111-4111-8111-111111111111");
    static readonly DateOnly Day=new(2026,10,12);
    static readonly DateTimeOffset Now=new(2026,10,14,22,0,0,TimeSpan.Zero);
    public static PayrollState State(bool jobs=false,bool finance=false,bool scheduling=false)
    {
        var s=new PayrollState{Configuration=new(){Enabled=true,JobsEnabled=jobs,FinanceEnabled=finance,SchedulingEnabled=scheduling,WeeklyOvertime=40,DailyOvertime=8,DailyDoubleTime=12,EarningsMapping=new(){["REGULAR"]="REG",["OVERTIME"]="OT",["DOUBLE_TIME"]="DT"}}};
        s.Profiles.Add(new(Guid.NewGuid(),Employee,Day,"HOURLY",25,null,true,"Laborer","provider-1","Operations",35));s.Periods.Add(new(Guid.NewGuid(),Day,Day.AddDays(6),Day.AddDays(11),Now));return s;
    }
    static PayrollTimeEntry Time(decimal hours=8,DateOnly? date=null,Guid? employee=null)=>new(Guid.NewGuid(),employee??Employee,date??Day,"America/New_York",null,null,hours,null,"GENERAL","Laborer","REGULAR","MANUAL","DRAFT",SourceKey:Guid.NewGuid().ToString());
    static PayrollCommand Cmd(string action,Guid target=default)=>new(){Action=action,Reason="Verified source evidence",TargetId=target};
    static void Apply(PayrollState s,PayrollCommand c,string actor="reviewer")=>PayrollEngine.Apply(s,c,actor,Now);
    static void Approve(PayrollState s,PayrollTimeEntry t){Apply(s,new(){Action="manual",Time=t,Reason="Source hours"});Apply(s,Cmd("submit",t.Id),Employee.ToString("D"));Apply(s,Cmd("approve",t.Id));}
    static PayrollBatch Batch(PayrollState s){Apply(s,Cmd("prepare",s.Periods[0].Id));return s.Batches.Single();}
    static PayrollActual Actual(PayrollBatch b,decimal hours=8,decimal gross=200)=>new(Guid.NewGuid(),b.Id,"provider-run-1","provider:line:1",Employee,"REGULAR",hours,gross,EmployerTaxes:20,Benefits:0,Burden:0);

    [Theory][InlineData(false,false,false)][InlineData(true,false,false)][InlineData(false,true,false)][InlineData(true,true,true)]
    public void ModuleCombinationsPrepareExportAndReconcileWithoutOtherModules(bool jobs,bool finance,bool scheduling)
    {
        var s=State(jobs,finance,scheduling);Approve(s,Time());var batch=Batch(s);Apply(s,Cmd("export",batch.Id));var artifact=s.Batches[0].Artifact;Apply(s,new(){Action="import-results",TargetId=batch.Id,Actuals=[Actual(batch)],Reason="Provider return"});Apply(s,Cmd("reconcile",batch.Id));Assert.Equal("PROCESSED",s.Batches[0].Status);Assert.Equal(artifact,s.Batches[0].Artifact);Assert.Contains(s.Events,e=>e.Type=="PayrollBatchProcessed");
    }
    [Fact]public void FinanceOnlyStillPostsExternalManualLaborWithoutPayroll()
    {var s=FinanceTests.State();s.Operations.Rates.Add(new(Guid.NewGuid(),Employee,new(2026,9,1),30,5,"owner"));var t=new FinanceTimeEntry(Guid.NewGuid(),"external:payroll:1",Employee,Guid.NewGuid(),new(2026,9,15),8,"LABOR","DRAFT");FinanceOperationsEngine.Apply(s,new(){Action="submit-time",Time=t},"owner");FinanceOperationsEngine.Apply(s,new(){Action="approve-time",TargetId=t.Id},"owner");Assert.Equal("APPROVED",s.Operations.Time[0].Status);Assert.Empty(s.Journals);}
    [Fact]public void OneActiveClockAndAdjacentSwitchPreserveContinuity()
    {
        var s=State();var t=Time() with{ClockIn=null};var at=new DateTimeOffset(2026,10,12,11,0,0,TimeSpan.Zero);Apply(s,new(){Action="clock-in",Time=t,At=at,Reason="Clock"});Assert.Throws<ArgumentException>(()=>Apply(s,new(){Action="clock-in",Time=Time(),At=at,Reason="Duplicate clock"}));var next=Time();Apply(s,new(){Action="switch",TargetId=t.Id,Time=next,At=at.AddHours(3),Reason="Switch task"});Assert.Equal(at.AddHours(3),s.Time[0].ClockOut);Assert.Equal(s.Time[0].ClockOut,s.Time[1].ClockIn);Assert.Equal(3,s.Time[0].Hours);Assert.Equal("ACTIVE",s.Time[1].Status);
    }
    [Fact]public void PaidAndUnpaidBreaksUseOriginalTimestamps()
    {
        var s=State();var t=Time();var at=new DateTimeOffset(2026,10,12,11,0,0,TimeSpan.Zero);Apply(s,new(){Action="clock-in",Time=t,At=at,Reason="Clock"});Apply(s,new(){Action="start-break",TargetId=t.Id,At=at.AddHours(2),BreakKind="UNPAID",Reason="Meal"});Assert.Throws<ArgumentException>(()=>Apply(s,new(){Action="clock-out",TargetId=t.Id,At=at.AddHours(8),Reason="Open break"}));Apply(s,new(){Action="end-break",TargetId=t.Id,At=at.AddHours(2.5),Reason="Meal ends"});Apply(s,new(){Action="start-break",TargetId=t.Id,At=at.AddHours(4),BreakKind="PAID",Reason="Paid rest"});Apply(s,new(){Action="end-break",TargetId=t.Id,At=at.AddHours(4.25),Reason="Rest ends"});Apply(s,new(){Action="clock-out",TargetId=t.Id,At=at.AddHours(8),Reason="End shift"});Assert.Equal(7.5m,s.Time[0].Hours);Assert.Equal(2,s.Time[0].Breaks!.Length);
    }
    [Fact]public void InvalidTimezoneAndClockOrderAreRejected()
    {var s=State();Assert.Throws<TimeZoneNotFoundException>(()=>PayrollEngine.ValidateTime(s,Time() with{Timezone="not-a-zone"}));var t=Time() with{ClockIn=Now,ClockOut=Now.AddMinutes(-1)};Assert.Throws<ArgumentException>(()=>PayrollEngine.ValidateTime(s,t));}
    [Fact]public void DaylightSavingClockUsesElapsedUtcHoursAndSplitsLocalDates()
    {
        var s=State();s.Profiles[0]=s.Profiles[0] with{EffectiveDate=new(2026,1,1)};var date=new DateOnly(2026,11,1);s.Periods.Clear();var t=Time(date:date) with{ClockIn=DateTimeOffset.Parse("2026-11-01T00:00:00-04:00"),ClockOut=DateTimeOffset.Parse("2026-11-01T04:00:00-05:00"),Status="APPROVED"};s.Time.Add(t with{Hours=PayrollEngine.Duration(t)});Assert.Equal(5,s.Time[0].Hours);Assert.Equal(5,PayrollEngine.Calculate(s,new(Guid.NewGuid(),date,date,date,Now)).Sum(l=>l.Hours));
    }
    [Fact]public void OvernightShiftClassifiesOnActualLocalWorkDays()
    {var s=State();var t=Time() with{ClockIn=DateTimeOffset.Parse("2026-10-12T22:00:00-04:00"),ClockOut=DateTimeOffset.Parse("2026-10-13T06:00:00-04:00"),Status="APPROVED"};s.Time.Add(t);var lines=PayrollEngine.Calculate(s,s.Periods[0]);Assert.Equal(2,lines.Single(l=>l.Date==Day).Hours);Assert.Equal(6,lines.Single(l=>l.Date==Day.AddDays(1)).Hours);}
    [Fact]public void DailyWeeklyAndDoubleTimeAreDeterministicWithoutMutatingRawTime()
    {var s=State();for(var i=0;i<5;i++)Approve(s,Time(10,Day.AddDays(i)));Approve(s,Time(14,Day.AddDays(5)));var before=PayrollEngine.Hash(s.Time);var lines=PayrollEngine.Calculate(s,s.Periods[0]);Assert.Equal(40,lines.Where(l=>l.Type=="REGULAR").Sum(l=>l.Hours));Assert.Equal(22,lines.Where(l=>l.Type=="OVERTIME").Sum(l=>l.Hours));Assert.Equal(2,lines.Where(l=>l.Type=="DOUBLE_TIME").Sum(l=>l.Hours));Assert.Equal(before,PayrollEngine.Hash(s.Time));}
    [Fact]public void OvertimeUsesEarlierApprovedDaysOutsidePartialPayrollPeriod()
    {var s=State();s.Configuration.DailyOvertime=null;s.Configuration.DailyDoubleTime=null;for(var i=0;i<5;i++)Approve(s,Time(10,Day.AddDays(i)));var lines=PayrollEngine.Calculate(s,new(Guid.NewGuid(),Day.AddDays(4),Day.AddDays(4),Day.AddDays(6),Now));Assert.Equal(10,lines.Single().Hours);Assert.Equal("OVERTIME",lines.Single().Type);}
    [Fact]public void CrewGeneratesSeparateEntriesWithIndividualOverride()
    {var s=State();var other=Guid.NewGuid();var crew=new PayrollCrew(Guid.NewGuid(),"Crew A",Employee,[Employee,other],Day);Apply(s,new(){Action="crew",Crew=crew,Reason="Assigned existing members"});Apply(s,new(){Action="crew-time",TargetId=crew.Id,Entries=[Time(),Time(6,employee:other)],Reason="Crew exceptions recorded"});Assert.Equal(2,s.Time.Count);Assert.Equal(14,s.Time.Sum(t=>t.Hours));Assert.All(s.Time,t=>Assert.Equal("CREW",t.Source));}
    [Fact]public void CorrectionPreservesOriginalAndRequiresNewApproval()
    {var s=State();var t=Time();Approve(s,t);var before=JobConfigurationService.Clone(s);var replacement=Time(7);Apply(s,new(){Action="correct",TargetId=t.Id,Time=replacement,Reason="Verified early departure"});PayrollIntegrity.ValidateTransition(before,s);Assert.Equal(8,s.Time[0].Hours);Assert.Equal("CORRECTED",s.Time[0].Status);Assert.Equal(t.Id,s.Time[1].CorrectsId);Assert.Equal("DRAFT",s.Time[1].Status);Assert.Null(s.Time[1].ApprovedAt);}
    [Fact]public void ApprovedTimeTamperingIsRejectedAtStorageBoundary()
    {var s=State();Approve(s,Time());var modified=JobConfigurationService.Clone(s);modified.Time[0]=modified.Time[0] with{Hours=9};Assert.Throws<ArgumentException>(()=>PayrollIntegrity.ValidateTransition(s,modified));}
    [Fact]public void ExportedBatchIsImmutableAndCanOnlyBeAdjustedLater()
    {var s=State();var t=Time();Approve(s,t);var b=Batch(s);Apply(s,Cmd("export",b.Id));Assert.Throws<ArgumentException>(()=>Apply(s,new(){Action="correct",TargetId=t.Id,Time=Time(6),Reason="Late edit"}));s.Periods.Add(new(Guid.NewGuid(),Day.AddDays(7),Day.AddDays(13),Day.AddDays(18),Now));var correction=Time(-2,Day.AddDays(7));Apply(s,new(){Action="adjustment",TargetId=t.Id,Time=correction,Reason="Next period adjustment"});Apply(s,Cmd("submit",correction.Id));Apply(s,Cmd("approve",correction.Id));Assert.Equal(8,s.Time[0].Hours);Assert.Equal(-2,PayrollEngine.Calculate(s,s.Periods[1]).Sum(l=>l.Hours));var copy=JobConfigurationService.Clone(s);copy.Batches[0]=copy.Batches[0] with{Artifact="modified"};Assert.Throws<ArgumentException>(()=>PayrollIntegrity.ValidateTransition(s,copy));}
    [Fact]public void SelfApprovalAndUnreviewedBatchAreRejected()
    {var s=State();var t=Time();Apply(s,new(){Action="manual",Time=t,Reason="Manual"});Assert.Throws<ArgumentException>(()=>Batch(s));Apply(s,Cmd("submit",t.Id));Assert.Throws<ArgumentException>(()=>Apply(s,Cmd("approve",t.Id),Employee.ToString("D")));}
    [Fact]public void ProviderImportIsIdempotentAndConflictingIdentityIsRejected()
    {var s=State();Approve(s,Time());var b=Batch(s);Apply(s,Cmd("export",b.Id));var a=Actual(b);var c=new PayrollCommand{Action="import-results",TargetId=b.Id,Actuals=[a],Reason="Provider data"};Apply(s,c);Apply(s,c);Assert.Single(s.Actuals);Assert.Single(s.Events,e=>e.Type=="PayrollActualReceived");c.Actuals=[a with{Hours=9}];Assert.Throws<ArgumentException>(()=>Apply(s,c));}
    [Fact]public void ProviderMismatchCannotBeSilentlyReconciled()
    {var s=State();Approve(s,Time());var b=Batch(s);Apply(s,Cmd("export",b.Id));Apply(s,new(){Action="import-results",TargetId=b.Id,Actuals=[Actual(b,7,175)],Reason="Provider mismatch"});Assert.Contains(PayrollEngine.Reconcile(s,b),i=>i.Contains("mismatch"));Assert.Throws<ArgumentException>(()=>Apply(s,Cmd("reconcile",b.Id)));}
    [Fact]public void SalaryIsAllocatedOnceWithoutHourlyRateSubstitution()
    {var s=State();s.Profiles[0]=s.Profiles[0] with{PayType="SALARY",PayRate=null,SalaryPerPeriod=1000,OvertimeEligible=false};Approve(s,Time());Approve(s,Time(8,Day.AddDays(1)));var b=Batch(s);Assert.Equal(1000,b.Lines.Sum(l=>l.Gross));Assert.All(b.Lines,l=>Assert.Null(l.Rate));}
    [Fact]public void GenericCsvNeutralizesFormulaIdentifiers()
    {var s=State();s.Profiles[0]=s.Profiles[0] with{ProviderEmployeeId="\uFEFF =SUM(A1:A2)"};Approve(s,Time());var csv=new GenericCsvPayrollProvider().PrepareExport(Batch(s));Assert.Contains("'\uFEFF =SUM",csv);}
    [Fact]public void PayrollDisabledDoesNotAcceptTimeOrInferLegalOvertime()
    {var s=new PayrollState();Assert.Null(s.Configuration.WeeklyOvertime);Assert.Throws<ArgumentException>(()=>Apply(s,new(){Action="manual",Time=Time(),Reason="Disabled"}));}
    [Fact]public void PayrollGrantsAreIndependentAndCompensationDoesNotComeFromPeople()
    {var member=new TenantMembership{Role="staff",MembershipStatus="Active"};Assert.DoesNotContain("payroll.read",UserModulePermissions.Resolve(member,null));var profile=new UserProfile{IsActive=true,ModulePermissions=["payroll.read","payroll.write","time.self","users.read","payroll.compensation.read"]};var grants=UserModulePermissions.Resolve(member,profile);Assert.Contains("time.self",grants);Assert.DoesNotContain("payroll.compensation.read",grants);Assert.DoesNotContain("finance.read",grants);Assert.Equal("payroll",MedInsights.API.Infrastructure.UserModuleAccessFilter.ModuleFor("Payroll"));}
    [Fact]public void OptionalFinanceEstimateAndActualDeltaNeverDoublePost()
    {
        var p=State(true,true,true);var job=Guid.NewGuid();Approve(p,Time() with{JobId=job,CostCode="GENERAL"});var b=Batch(p);var f=FinanceTests.State();f.Periods.Clear();f.Periods.Add(new(){Id="2026-10",Start=new(2026,10,1),End=new(2026,10,31)});f.CostCodes.Add(new("GENERAL","General labor","LABOR"));f.Accounts.Add(new("2200","Payroll accrual","LIABILITY","CREDIT"));var c=new PayrollFinanceCommand{BatchId=b.Id,Date=Day,LiabilityAccount="2200",Reason="Approved labor estimate"};PayrollFinanceConsumer.Apply(f,p,b,c,"owner");PayrollFinanceConsumer.Apply(f,p,b,c,"owner");Assert.Single(f.Journals);Assert.Equal(280,f.Costs.Sum(v=>v.Amount));Apply(p,Cmd("export",b.Id));Apply(p,new(){Action="import-results",TargetId=b.Id,Actuals=[Actual(b)],Reason="Provider return"});Apply(p,Cmd("reconcile",b.Id));c.Stage="actual";PayrollFinanceConsumer.Apply(f,p,p.Batches[0],c,"owner");PayrollFinanceConsumer.Apply(f,p,p.Batches[0],c,"owner");Assert.Equal(2,f.Journals.Count);Assert.Equal(220,f.Costs.Sum(v=>v.Amount));FinanceIntegrity.ValidateTransition(new(),f);
    }
    [Fact]public async Task PayrollServiceDoesNotCallJobsOrCalendarWhenDisabledAndRedactsCompensation()
    {var f=new Fixture();f.State=State();Approve(f.State,Time());var root=JsonSerializer.SerializeToElement(await f.Service.ReadAsync(),JobConfigurationService.Json);Assert.Empty(root.GetProperty("profiles").EnumerateArray());Assert.Empty(root.GetProperty("batches").EnumerateArray());Assert.DoesNotContain("payRate",root.GetRawText());f.Jobs.VerifyNoOtherCalls();f.Calendar.VerifyNoOtherCalls();}
    [Fact]public async Task ServiceRejectsUnauthorizedApprovalAndCrossTenantEmployee()
    {var f=new Fixture();await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Service.CommandAsync(Cmd("approve",Guid.NewGuid())));await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync(new(){Action="manual",Time=Time(employee:Guid.NewGuid()),Reason="Foreign identity"}));Assert.Empty(f.State.Time);}
    [Fact]public async Task ServiceRequiresExactVersionAndMakesRetryIdempotent()
    {var f=new Fixture();var command=new PayrollCommand{Action="manual",Time=Time(),Reason="Manual evidence"};await f.Service.CommandAsync(command);await f.Service.CommandAsync(command);Assert.Single(f.State.Time);command.Reason="Different source";await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync(command));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.CommandAsync(new(){Action="manual",Time=Time(),Reason="Stale browser"}));}
    [Fact]public async Task ApprovedLeaveBlocksSchedulingOnlyWhenIntegrationEnabled()
    {var f=new Fixture();f.State.Configuration.SchedulingEnabled=true;f.State.Leave.Add(new(Guid.NewGuid(),Employee,Day,8,"PTO","APPROVED"));var availability=new PayrollAvailability(f.Store.Object,f.Members.Object);await Assert.ThrowsAsync<ArgumentException>(()=>availability.RequireAvailableAsync(f.Tenant,[f.Membership.Id],Day.ToDateTime(new(12,0),DateTimeKind.Utc),Day.ToDateTime(new(20,0),DateTimeKind.Utc)));f.State.Configuration.SchedulingEnabled=false;await availability.RequireAvailableAsync(f.Tenant,[f.Membership.Id],Day.ToDateTime(new(12,0),DateTimeKind.Utc),Day.ToDateTime(new(20,0),DateTimeKind.Utc));}

    [Fact]public void VoidUnexportedBatchAllowsNewImmutableVersion()
    {var s=State();Approve(s,Time());var b=Batch(s);Apply(s,Cmd("void-batch",b.Id));Apply(s,Cmd("prepare",s.Periods[0].Id));Assert.Equal(2,s.Batches.Count);Assert.Equal("VOID",s.Batches[0].Status);Assert.Equal(2,s.Batches[1].ExportVersion);Assert.Equal(b.Lines,s.Batches[0].Lines);}
    [Fact]public void ReviewedProviderCorrectionPreservesOriginalAndReconcilesReplacement()
    {var s=State();Approve(s,Time());var b=Batch(s);Apply(s,Cmd("export",b.Id));var wrong=Actual(b,7,175);Apply(s,new(){Action="import-results",TargetId=b.Id,Actuals=[wrong],Reason="Provider return"});var corrected=Actual(b) with{SourceKey="provider:correction:1",CorrectsId=wrong.Id};Apply(s,new(){Action="correct-result",TargetId=b.Id,Actuals=[corrected],Reason="Provider verified correction"});Assert.Equal(2,s.Actuals.Count);Assert.Contains(wrong,s.Actuals);Assert.Empty(PayrollEngine.Reconcile(s,s.Batches[0]));Apply(s,Cmd("reconcile",b.Id));}
    [Fact]public void PaidLeaveCreatesTimeForPayrollReviewWithoutRequiringAJob()
    {var s=State(true);s.Configuration.RequireJob=true;var lv=new PayrollLeave(Guid.NewGuid(),Employee,Day,8,"PTO",Reason:"Approved vacation request");Apply(s,new(){Action="leave",Leave=lv,Reason="Request"});Apply(s,Cmd("approve-leave",lv.Id));Assert.Equal("APPROVED",s.Leave[0].Status);Assert.Equal("SUBMITTED",s.Time[0].Status);Assert.Equal("PTO",s.Time[0].EarningType);Assert.Null(s.Time[0].JobId);Apply(s,Cmd("approve",s.Time[0].Id));var lines=PayrollEngine.Calculate(s,s.Periods[0]);Assert.Equal("PTO",Assert.Single(lines).Type);Assert.Equal(8,lines[0].Hours);}
    [Fact]public void ExportedAdjustmentKeepsOriginalPayRateAfterRateChange()
    {var s=State();var t=Time();Approve(s,t);var b=Batch(s);Apply(s,Cmd("export",b.Id));var next=Day.AddDays(7);s.Periods.Add(new(Guid.NewGuid(),next,next.AddDays(6),next.AddDays(11),Now));s.Profiles.Add(s.Profiles[0] with{Id=Guid.NewGuid(),EffectiveDate=next,PayRate=100});var adjustment=Time(-2,next);Apply(s,new(){Action="adjustment",TargetId=t.Id,Time=adjustment,Reason="Reverse exported hours"});Apply(s,Cmd("submit",adjustment.Id));Apply(s,Cmd("approve",adjustment.Id));Assert.Equal(-50,PayrollEngine.Calculate(s,s.Periods[1]).Single().Gross);}
    [Fact]public async Task BobReadinessExcludesCompensationAndRejectsMismatchedActor()
    {var f=new Fixture();var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(f.Tenant);user.SetupGet(u=>u.UserId).Returns(Employee);var provider=new BobPayrollActionProvider(f.Service,user.Object,"payroll.readiness");var result=JsonSerializer.Serialize(await provider.ExecuteAsync(new(f.Tenant,Employee,Guid.NewGuid(),"tenant"),JsonSerializer.SerializeToElement(new{})));Assert.DoesNotContain("PayRate",result);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>provider.ExecuteAsync(new(Guid.NewGuid(),Employee,Guid.NewGuid(),"tenant"),JsonSerializer.SerializeToElement(new{})));}

    [Fact]public async Task EnabledJobsValidateTenantPartitionAndAllowOperationalAllocationWithoutFinance()
    {var f=new Fixture();f.State.Configuration.JobsEnabled=true;var job=Guid.NewGuid();var partition=RepositoryKeyHelper.ToTenantPartitionKey(f.Tenant);f.Jobs.Setup(j=>j.GetAsync(partition,RepositoryKeyHelper.ToRowKey(job),It.IsAny<CancellationToken>())).ReturnsAsync(new Job{Id=job,PartitionKey="foreign-tenant"});var c=new PayrollCommand{Action="manual",Time=Time() with{JobId=job},Reason="Operational job allocation"};await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync(c));Assert.Empty(f.State.Time);f.Jobs.Setup(j=>j.GetAsync(partition,RepositoryKeyHelper.ToRowKey(job),It.IsAny<CancellationToken>())).ReturnsAsync(new Job{Id=job,PartitionKey=partition});await f.Service.CommandAsync(c);Assert.Equal(job,f.State.Time.Single().JobId);f.Calendar.VerifyNoOtherCalls();}
    [Fact]public async Task DeploymentDisabledAvailabilityNeverTouchesPayrollStorage()
    {var store=new Mock<IPayrollStore>(MockBehavior.Strict);var configuration=new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();var availability=new PayrollAvailability(store.Object,Mock.Of<ITenantMembershipRepository>(),configuration);await availability.RequireAvailableAsync(Guid.NewGuid(),[Guid.NewGuid()],Day.ToDateTime(new(12,0),DateTimeKind.Utc),Day.ToDateTime(new(20,0),DateTimeKind.Utc));store.VerifyNoOtherCalls();}

    [Fact]public void ConfiguredMealWarningDoesNotInventOrDeductWorkedTime()
    {var s=State();s.Configuration.MealBreakRequiredAfterHours=6;var t=Time(8);Assert.Contains(PayrollEngine.Exceptions(s,t),i=>i.Contains("meal-break"));Assert.Equal(8,t.Hours);}
    [Fact]public void EventLocationRequiresTenantPolicyAndExplicitOverride()
    {var s=State();var t=Time() with{Source="CLOCK",Status="ACTIVE",ClockIn=DateTimeOffset.Parse("2026-10-12T08:00:00-04:00"),Hours=0};Assert.Throws<ArgumentException>(()=>PayrollEngine.ValidateTime(s,t with{Location=new(40,-80,10,"Punch evidence")}));s.Configuration.LocationPolicy="REQUIRE_WITH_OVERRIDE";Assert.Throws<ArgumentException>(()=>PayrollEngine.ValidateTime(s,t));PayrollEngine.ValidateTime(s,t with{Notes="GPS unavailable; supervisor to review"});}
    [Fact]public void EnabledProductDoesNotForceIntegrationOrGrantPermissions()
    {var s=State(true,true,true);s.Configuration.IntegrateJobs=false;s.Configuration.IntegrateFinance=false;s.Configuration.IntegrateScheduling=false;Assert.False(s.Configuration.UsesJobs);Assert.False(s.Configuration.UsesFinance);Assert.False(s.Configuration.UsesScheduling);Approve(s,Time());Assert.Throws<ArgumentException>(()=>PayrollEngine.ValidateTime(s,Time() with{JobId=Guid.NewGuid()}));Assert.False(UserModulePermissions.Allows([],"payroll",true));}

    [Fact]public void MissingSalaryAllocationBlocksBatchInsteadOfSilentlyOmittingEmployee()
    {var s=State();s.Profiles.Add(s.Profiles[0] with{Id=Guid.NewGuid(),EmployeeId=Guid.NewGuid(),PayType="SALARY",PayRate=null,SalaryPerPeriod=1000,OvertimeEligible=false});Approve(s,Time());Assert.Throws<ArgumentException>(()=>Batch(s));}
    [Fact]public async Task BobPrepareRechecksActionAndCompensationPermissions()
    {var f=new Fixture();var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(f.Tenant);user.SetupGet(u=>u.UserId).Returns(Employee);var provider=new BobPayrollActionProvider(f.Service,user.Object,"payroll.prepare");var command=Cmd("prepare",f.State.Periods[0].Id);await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>provider.ExecuteAsync(new(f.Tenant,Employee,Guid.NewGuid(),"tenant"),JsonSerializer.SerializeToElement(command,JobConfigurationService.Json)));Assert.Empty(f.State.Batches);}
    sealed class Fixture
    {
        public Guid Tenant=Guid.NewGuid();public PayrollState State=PayrollTests.State();public Mock<IPayrollStore> Store=new();public Mock<IPayrollAuthority> Authority=new();public Mock<ITenantMembershipRepository> Members=new();public Mock<IJobRepository> Jobs=new(MockBehavior.Strict);public Mock<ICalendarEventRepository> Calendar=new(MockBehavior.Strict);public TenantMembership Membership;public PayrollService Service;
        public Fixture(){Membership=new(){Id=Guid.NewGuid(),UserId=Employee,TenantId=Tenant,MembershipStatus="Active",Role="staff"};var user=new Mock<IUserContext>();user.SetupGet(u=>u.TenantId).Returns(Tenant);user.SetupGet(u=>u.UserId).Returns(Employee);user.SetupGet(u=>u.IsAuthenticated).Returns(true);Authority.Setup(a=>a.AllowsAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string action,CancellationToken ct)=>action=="time.self");Members.Setup(m=>m.GetActiveAssignedByTenantAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(new[]{Membership});Store.Setup(s=>s.ReadAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>JobConfigurationService.Clone(State));Store.Setup(s=>s.SaveAsync(Tenant,It.IsAny<PayrollState>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Callback((Guid tenant,PayrollState s,string expected,CancellationToken ct)=>{Assert.Equal(State.Version,expected);s.Version=Guid.NewGuid().ToString();State=JobConfigurationService.Clone(s);}).Returns(Task.CompletedTask);Service=new(Store.Object,Authority.Object,user.Object,Members.Object,Jobs.Object,Calendar.Object);}
    }
}
