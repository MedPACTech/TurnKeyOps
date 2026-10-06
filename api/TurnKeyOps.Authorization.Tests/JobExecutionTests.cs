using System.Text.Json;
using Azure;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;
namespace MedInsights.Authorization.Tests;
public sealed class JobExecutionTests
{
    [Theory][InlineData("concrete")][InlineData("doors-locks")][InlineData("framing")][InlineData("land-clearing")]
    public void ProfilesExtendOneCoreAndSnapshotsDoNotShareMutableTemplates(string trade)
    {
        var a=JobConfigurationService.Initialize(new(){TradeType=JobExecutionRules.Trade(trade),Description="Sold scope"},new());
        var b=JobConfigurationService.Initialize(new(){TradeType=JobExecutionRules.Trade(trade)},new());
        a.Tasks[0].CompletedAtUtc=DateTime.UtcNow;
        Assert.Equal(trade,a.TradeProfile);Assert.Null(b.Tasks[0].CompletedAtUtc);Assert.Equal("Sold scope",a.SoldScope);
        Assert.NotEmpty(JobExecutionRules.Blockers(a,"start"));
    }
    [Fact]public void TenantLabelsNeverChangeCanonicalTransitions()
    {
        var job=new JobDto{Status=JobStatus.ReadyToStart,Execution=new(){Profile=new(){StateLabels=new(){["READY_TO_START"]="Ready to pour"}}}};
        JobExecutionRules.Transition(job,"IN_PROGRESS","",true);Assert.Equal(JobStatus.InProgress,job.Status);
        Assert.Throws<ArgumentException>(()=>JobExecutionRules.Transition(job,"Ready to pour","",true));
    }
    [Fact]public void ReadinessAndAcceptancePreventPrematureClosure()
    {
        var job=new JobDto{Status=JobStatus.CompletionReview,Execution=new(){Tasks=[new(){Title="Inspection",Required=true}]}};
        Assert.Throws<ArgumentException>(()=>JobExecutionRules.Transition(job,"COMPLETED","",true));
        job.Execution.Tasks[0].CompletedAtUtc=DateTime.UtcNow;JobExecutionRules.Transition(job,"COMPLETED","",true);
        Assert.Throws<ArgumentException>(()=>JobExecutionRules.Transition(job,"CLOSED","",true));
        job.Execution.Acceptances.Add(new(){CompletionRevision=1});JobExecutionRules.Transition(job,"CLOSED","",true);
        Assert.Throws<ArgumentException>(()=>JobExecutionRules.Transition(job,"PLANNING","",true));
        JobExecutionRules.Transition(job,"PLANNING","Return visit requested",true);Assert.Equal(2,job.Execution.CompletionRevision);
    }
    [Fact]public void MaterialAndIssuesBlockStartButMaterialsDoNotBlockPlanning()
    {
        var x=new JobExecutionDto{Requirements=[new(){Description="Door",Status="Ordered"}]};
        Assert.Empty(JobExecutionRules.Blockers(x,"schedule"));Assert.Contains(JobExecutionRules.Blockers(x,"start"),s=>s.Contains("Door"));
        x.Requirements[0].Status="Delivered";Assert.Empty(JobExecutionRules.Blockers(x,"start"));
        x.Issues.Add(new(){Description="Site inaccessible"});Assert.NotEmpty(JobExecutionRules.Blockers(x,"schedule"));
    }
    [Fact]public async Task TenantMismatchCannotBeReadOrMutated()
    {
        var f=new Fixture();f.Job.PartitionKey="TENANT|foreign";
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.WorkspaceAsync(f.Job.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Command("activity",text:"test"));
        f.Jobs.Verify(j=>j.SaveAsync(It.IsAny<Job>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]public async Task ReadOnlyAuthorityCannotMutateEvenThroughBobProvider()
    {
        var f=new Fixture();f.Authority.Setup(a=>a.RequireAsync(true,It.IsAny<CancellationToken>())).ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("Read only"));
        await f.Service.WorkspaceAsync(f.Job.Id);
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Command("activity",text:"Unauthorized"));
        var provider=new BobJobActionProvider(f.Service,null!,"job.activity");
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>provider.ExecuteAsync(null!,JsonSerializer.SerializeToElement(new{jobId=f.Job.Id,expectedVersion=f.Job.ETag.ToString(),text="Unauthorized"})));
    }
    [Fact]public async Task StaleMutationCannotOverwriteAuditOrScope()
    {
        var f=new Fixture();await f.Command("activity",text:"Installed approved hardware");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.CommandAsync(f.Job.Id,new(){ExpectedVersion="stale",Action="activity",Text="late write"}));
        Assert.Equal("Customer approved scope",f.Payload.Execution!.SoldScope);Assert.Single(f.Payload.Activity);
    }
    [Fact]public async Task EvidenceMustBelongToJobAndDependenciesMustComplete()
    {
        var f=new Fixture();var predecessor=new JobTaskDto{Title="Verify"};var task=new JobTaskDto{Title="Install",DependsOn=[predecessor.Id],EvidenceRequired=true};f.Payload.Execution!.Tasks=[predecessor,task];
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Command("task",task.Id));
        predecessor.CompletedAtUtc=DateTime.UtcNow;
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync(f.Job.Id,new(){ExpectedVersion=f.Job.ETag.ToString(),Action="task",ItemId=task.Id,EvidenceIds=[Guid.NewGuid()]}));
    }
    [Fact]public async Task ChangeCannotJumpToImplementedOrInventApprovedPrice()
    {
        var f=new Fixture();var c=new JobChangeDto{Description="Extra opening",PricingImpact=true};f.Payload.Execution!.Changes.Add(c);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Command("change-status",c.Id,state:"IMPLEMENTED"));
        await f.Command("change-status",c.Id,state:"REVIEW");await f.Command("change-status",c.Id,state:"PRICING_REQUIRED");await f.Command("change-status",c.Id,state:"APPROVAL_REQUIRED");
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Command("change-status",c.Id,state:"APPROVED"));
        Assert.Equal("Customer approved scope",f.Payload.Execution!.SoldScope);
    }
    [Fact]public async Task AcceptedChangeEstimateMustMatchCustomerAndSite()
    {
        var f=new Fixture();var c=new JobChangeDto{Description="Extra work",Status="APPROVAL_REQUIRED",PricingImpact=true};f.Payload.Execution!.Changes.Add(c);
        var estimate=Guid.NewGuid();f.Estimates.Setup(e=>e.GetAsync(estimate,It.IsAny<CancellationToken>())).ReturnsAsync(new QuoteEstimateDto{CustomerId=Guid.NewGuid(),Document=new(){SiteId=f.Job.JobSiteId},Delivery=new(){Status="approved"}});
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CommandAsync(f.Job.Id,new(){ExpectedVersion=f.Job.ETag.ToString(),Action="change-status",ItemId=c.Id,State="APPROVED",Change=new(){AcceptedEstimateId=estimate}}));
    }
    [Fact]public async Task CalendarConflictsAndForeignMembershipPreventSchedule()
    {
        var f=new Fixture();var start=DateTime.UtcNow.AddDays(1);var member=Guid.NewGuid();f.Members.Setup(m=>m.GetActiveAssignedByTenantAsync(f.Tenant,It.IsAny<CancellationToken>())).ReturnsAsync([new(){Id=member,TenantId=f.Tenant,Role="staff"}]);
        var input=new JobEventInputDto{ExpectedVersion=f.Job.ETag.ToString(),Title="Install",StartUtc=start,EndUtc=start.AddHours(1),MembershipIds=[member]};
        f.Events.Add(new(){Id=Guid.NewGuid(),PartitionKey=f.Partition,MembershipIds=[member],StartUtc=start,EndUtc=start.AddHours(2)});
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ScheduleAsync(f.Job.Id,input));
        f.Events.Clear();input.MembershipIds=[Guid.NewGuid()];await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ScheduleAsync(f.Job.Id,input));
    }
    [Fact]public async Task MultipleEventsShareExistingCalendarAndAuditConfirmation()
    {
        var f=new Fixture();var member=Guid.NewGuid();f.Members.Setup(m=>m.GetActiveAssignedByTenantAsync(f.Tenant,It.IsAny<CancellationToken>())).ReturnsAsync([new(){Id=member,TenantId=f.Tenant,Role="staff"}]);
        var now=DateTime.UtcNow.AddDays(1);
        for(var i=0;i<2;i++)await f.Service.ScheduleAsync(f.Job.Id,new(){ExpectedVersion=f.Job.ETag.ToString(),Title=$"Event {i}",StartUtc=now.AddHours(i*3),EndUtc=now.AddHours(i*3+1),MembershipIds=[member]});
        Assert.Equal(2,f.Events.Count);Assert.All(f.Events,e=>Assert.Equal(f.Job.Id,e.JobId));Assert.Equal(2,f.Payload.Activity.Count(a=>a.Type=="job.scheduled"));
    }
    [Fact]public async Task CompletionAcceptanceIsSeparateAndServerStamped()
    {
        var f=new Fixture();f.Job.Status=JobStatus.Completed;
        await f.Service.CommandAsync(f.Job.Id,new(){ExpectedVersion=f.Job.ETag.ToString(),Action="accept",Acceptance=new(){Signer="Customer",Statement="Work accepted",Signature="Customer",AcceptedAtUtc=DateTime.MinValue,CompletionRevision=999}});
        Assert.Single(f.Payload.Execution!.Acceptances);Assert.Equal(1,f.Payload.Execution.Acceptances[0].CompletionRevision);Assert.True(f.Payload.Execution.Acceptances[0].AcceptedAtUtc>DateTime.UtcNow.AddMinutes(-1));Assert.Null(f.Payload.AcceptedEstimate);
    }
    [Fact]public void AcceptedScopeSeedsOperationalRequirementsWithoutChangingSoldSnapshot()
    {
        var sold=new AcceptedEstimateDto{Revision=4,Document=new(){TradeProfile="doors-locks",Scope="Install approved opening"},SelectedOptions=[new(){Lines=[new(){Kind="material",Name="Door",CatalogId="door-1",Quantity=2,Unit="each"},new(){Kind="equipment",Name="Lift",Quantity=1,Unit="day"},new(){Kind="labor",Name="Installation",Quantity=3,Unit="hour"}]}]};
        var execution=JobConfigurationService.Initialize(new(){AcceptedEstimate=sold,RequiredDepositPercent=25},new());
        Assert.Equal("Won Estimate",execution.Origin);Assert.Equal(sold.Document.Scope,execution.SoldScope);Assert.Equal(2,execution.Requirements.Count);
        Assert.Equal(2,execution.Requirements[0].Quantity);Assert.Contains("4",execution.Requirements[0].Source);Assert.Contains(execution.Tasks,t=>t.TemplateSource=="sold-terms"&&t.EvidenceRequired);
        execution.Requirements[0].Quantity=10;Assert.Equal(2,sold.SelectedOptions[0].Lines[0].Quantity);
    }
    [Fact]public void PlanningPhotosCannotSatisfyCompletionEvidence()
    {
        var x=new JobExecutionDto{Profile=new(){CompletionPhotos=1},Evidence=[new(){ContentType="image/jpeg",Purpose="planning"}]};
        Assert.Contains(JobExecutionRules.Blockers(x,"complete"),b=>b.Contains("completion photos"));
        x.Evidence[0].Purpose="completion";Assert.Empty(JobExecutionRules.Blockers(x,"complete"));
    }
    [Fact]public async Task ChangingAcceptedCompletionRequiresNewSignoff()
    {
        var f=new Fixture();f.Job.Status=JobStatus.Completed;
        await f.Service.CommandAsync(f.Job.Id,new(){ExpectedVersion=f.Job.ETag.ToString(),Action="accept",Acceptance=new(){Signer="Customer",Statement="Work accepted",Signature="Customer"}});
        Assert.Equal(64,f.Payload.Execution!.Acceptances[0].CompletionHash.Length);
        await f.Service.CommandAsync(f.Job.Id,new(){ExpectedVersion=f.Job.ETag.ToString(),Action="issue",Issue=new(){Description="Review finish",Severity="info"}});
        Assert.Equal(2,f.Payload.Execution!.CompletionRevision);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Command("transition",state:"CLOSED"));
    }
    [Fact]public async Task SchedulingRequiresCalendarPermissionBeforeWriting()
    {
        var f=new Fixture();f.Authority.Setup(a=>a.RequireCalendarAsync(true,It.IsAny<CancellationToken>())).ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("Calendar is read only"));
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Service.ScheduleAsync(f.Job.Id,new(){ExpectedVersion=f.Job.ETag.ToString(),Title="Install",StartUtc=DateTime.UtcNow,EndUtc=DateTime.UtcNow.AddHours(1)}));
        Assert.Empty(f.Events);f.Jobs.Verify(j=>j.SaveAsync(It.IsAny<Job>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact]public async Task CalendarDatesOverrideAStaleJobSummaryAfterPartialFailure()
    {
        var f=new Fixture();f.Job.ScheduledStart=DateTime.UtcNow.AddDays(-7);f.Job.ScheduledEnd=DateTime.UtcNow.AddDays(-6);
        var start=DateTime.UtcNow.AddDays(2);var end=start.AddHours(2);
        f.Events.Add(new(){Id=Guid.NewGuid(),JobId=f.Job.Id,PartitionKey=f.Partition,StartUtc=start,EndUtc=end});
        var workspace=await f.Service.WorkspaceAsync(f.Job.Id);
        Assert.Equal(start,workspace.Job.ScheduledStart);Assert.Equal(end,workspace.Job.ScheduledEnd);
        f.Events[0].EventStatus="cancelled";
        Assert.Null((await f.Service.WorkspaceAsync(f.Job.Id)).Job.ScheduledStart);
    }
    private sealed class Fixture
    {
        public readonly Guid Tenant=Guid.NewGuid();public string Partition=>TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(Tenant);
        public Job Job;public JobWorkflowPayloadDto Payload;public readonly List<CalendarEvent> Events=[];
        public readonly Mock<IJobRepository> Jobs=new();public readonly Mock<IJobAuthority> Authority=new();public readonly Mock<ITenantMembershipRepository> Members=new();public readonly Mock<IQuoteEstimateService> Estimates=new();
        public readonly JobExecutionService Service;
        private int version=1;
        public Fixture(){
            Job=new(){Id=Guid.NewGuid(),PartitionKey=Partition,ETag=new ETag("v1"),Status=JobStatus.Planning,CustomerId=Guid.NewGuid(),JobSiteId=Guid.NewGuid()};Job.RowKey=TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(Job.Id);
            Payload=new(){Execution=new(){SoldScope="Customer approved scope"}};
            Jobs.Setup(j=>j.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Clone(Job));
            Jobs.Setup(j=>j.ListAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>new[]{Clone(Job)});
            Jobs.Setup(j=>j.SaveAsync(It.IsAny<Job>(),It.IsAny<CancellationToken>())).ReturnsAsync((Job j,CancellationToken _) =>{j.ETag=new ETag($"v{++version}");Job=Clone(j);return Clone(j);});
            var payloads=new Mock<IJobWorkflowPayloadStore>();var stored=new Dictionary<string,JobWorkflowPayloadDto>();
            payloads.Setup(p=>p.LoadAsync(It.IsAny<string?>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Clone(Payload));
            payloads.Setup(p=>p.SaveAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<JobWorkflowPayloadDto>(),It.IsAny<CancellationToken>())).ReturnsAsync((Guid _,Guid __,JobWorkflowPayloadDto p,CancellationToken ___)=>{Payload=Clone(p);return "blob";});
            var legacy=new Mock<IJobService>();legacy.Setup(l=>l.GetAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>{var dto=JobMapper.ToDto(Job);dto.Version=Job.ETag.ToString();dto.Execution=Clone(Payload.Execution);dto.Activity=Clone(Payload.Activity);return dto;});
            var calendar=new Mock<ICalendarEventRepository>();calendar.Setup(c=>c.ListAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Events.ToArray());calendar.Setup(c=>c.SaveAsync(It.IsAny<CalendarEvent>(),It.IsAny<CancellationToken>())).ReturnsAsync((CalendarEvent e,CancellationToken _) =>{Events.RemoveAll(x=>x.Id==e.Id);Events.Add(e);return e;});
            var settings=new Mock<ITenantSettingsRepository>();Authority.Setup(a=>a.CanWriteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);Authority.Setup(a=>a.IsOwnerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Members.Setup(m=>m.GetActiveAssignedByTenantAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync([]);
            Service=new(Jobs.Object,payloads.Object,legacy.Object,Authority.Object,new(settings.Object),new User(Tenant),calendar.Object,Members.Object,Mock.Of<ICustomerRepository>(),Mock.Of<IJobSiteRepository>(),Mock.Of<IAzureBlobStorageService>(),Estimates.Object,Mock.Of<IUserProfileRepository>());
        }
        public Task<JobWorkspaceDto> Command(string action,Guid? item=null,string text="",string state="")=>Service.CommandAsync(Job.Id,new(){Action=action,ItemId=item,Text=text,State=state,ExpectedVersion=Job.ETag.ToString()});
        private static T Clone<T>(T input){var copy=JobConfigurationService.Clone(input);if(input is Job source && copy is Job target)target.ETag=source.ETag;return copy;}
    }
    private sealed class User(Guid tenant):TurnKeyOps.Lib.Utils.IUserContext{public bool IsAuthenticated=>true;public Guid TenantId=>tenant;public Guid UserId=>Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");public AppTimeZone Timezone=>AppTimeZone.Utc;public string FirstName=>"Test";public string LastName=>"User";}
}
