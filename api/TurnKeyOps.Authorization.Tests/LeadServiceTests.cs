using System.Text.Json;
using MedInsights.Lib.Authorization;
using AppTimeZone = MedInsights.Lib.Utils.AppTimeZone;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;

public sealed class LeadServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly string Pk = RepositoryKeyHelper.ToTenantPartitionKey(Tenant);
    [Fact] public async Task MissingReadAndWritePermissionsFailBeforeAccessingRecords()
    {
        var f = new Fixture();
        f.Access.Setup(x => x.RequirePermissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("Denied"));
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => f.Service.WorkspaceAsync());
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => f.Service.CreateAsync(Input()));
        Assert.Empty(f.Store);
        f.Repository.Verify(x => x.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact] public async Task ReadOnlyCannotMutate()
    {
        var f = new Fixture();
        f.Access.Setup(x => x.RequirePermissionAsync(TurnKeyPermissionKeys.LeadsWrite, It.IsAny<CancellationToken>())).ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("Denied"));
        Assert.Empty((await f.Service.WorkspaceAsync()).Leads);
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(() => f.Service.CreateAsync(Input()));
    }
    [Fact] public async Task TenantIsolationRejectsForeignEntitiesEvenIfRepositoryReturnsThem()
    {
        var f = new Fixture();
        var foreign = Entity(Guid.NewGuid()); foreign.Data.TenantId = Guid.NewGuid();
        f.Repository.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(foreign);
        f.Store[foreign.Id] = foreign;
        Assert.Null(await f.Service.GetAsync(foreign.Id)); Assert.Empty((await f.Service.WorkspaceAsync()).Leads);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.NoteAsync(foreign.Id, new() { ExpectedVersion = "v1", Text = "No" }));
    }
    [Fact] public async Task ManualLeadsShareOneCustomerWithoutMergingOpportunities()
    {
        var f = new Fixture();var customer = Guid.NewGuid();
        f.Customers.Setup(x => x.GetAsync(Pk, RepositoryKeyHelper.ToRowKey(customer), It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { Id = customer, PartitionKey = Pk });
        var first = Input(); first.CustomerId = customer; var second = Input(); second.CustomerId = customer; second.Title = "Another project";
        var a = await f.Service.CreateAsync(first);var b = await f.Service.CreateAsync(second);
        Assert.NotEqual(a.Id,b.Id);Assert.Equal(a.CustomerId,b.CustomerId);Assert.Equal(2,f.Store.Count);
        f.Customers.Verify(x => x.SaveAsync(It.IsAny<Customer>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task LostCanReopenAndLabelsNeverChangeCanonicalSemantics()
    {
        var f = new Fixture(); f.Config = new() { StageLabels = new() { ["LOST"] = "Not proceeding" } };
        var lead = await f.Service.CreateAsync(Input());
        lead = await f.Service.StageAsync(lead.Id,new() { ExpectedVersion=lead.Version, Stage="LOST",Reason="Price" });
        Assert.Equal("LOST",lead.Stage);Assert.Equal("Not proceeding",lead.StageLabel);
        lead = await f.Service.StageAsync(lead.Id,new() { ExpectedVersion=lead.Version, Stage="QUALIFYING" });
        Assert.Equal("QUALIFYING",lead.Stage);Assert.Equal(3,lead.Activity.Count);Assert.NotNull(lead.LostAtUtc);
    }
    [Fact] public async Task QualificationGateAndStaleVersionsPreventChanges()
    {
        var f = new Fixture(); var input = Input();input.TradeProfile="concrete";input.SiteAddress="";
        var lead = await f.Service.CreateAsync(input);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.StageAsync(lead.Id,new(){ExpectedVersion=lead.Version,Stage="QUALIFIED"}));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.NoteAsync(lead.Id,new(){ExpectedVersion="stale",Text="No"}));
        Assert.Equal("NEW",f.Store[lead.Id].Data.Stage);Assert.Single(f.Store[lead.Id].Data.Activity);
    }
    [Fact] public async Task DuplicateSuggestionsNeverWriteOrMerge()
    {
        var f = new Fixture(); var lead = await f.Service.CreateAsync(Input());
        var candidate = new Customer { Id=Guid.NewGuid(),PartitionKey=Pk,Email=lead.Email,FirstName="Same contact" };
        f.Customers.Setup(x=>x.ListAsync(Pk,It.IsAny<CancellationToken>())).ReturnsAsync(new[]{candidate});
        var suggestions=await f.Service.DuplicatesAsync(lead.Id);
        Assert.Single(suggestions);Assert.Null(f.Store[lead.Id].Data.CustomerId);
        f.Customers.Verify(x=>x.SaveAsync(It.IsAny<Customer>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task IntakeBridgeIsTenantBoundAndDoesNotResetProgressOnRetry()
    {
        var f=new Fixture();var id=Guid.NewGuid();var request=new QuoteRequest{Id=id,TenantId=Tenant,PartitionKey=Pk,Source="public-site",ContactName="Test",SubmittedAtUtc=DateTime.UtcNow,DateUpdated=DateTime.UtcNow};
        var first=await f.Bridge.EnsureAsync(request);first.Data.Stage="FOLLOW_UP";
        await f.Bridge.EnsureAsync(request);
        Assert.Single(f.Store);Assert.Equal("FOLLOW_UP",f.Store[id].Data.Stage);Assert.Equal("Website",first.Data.Source);Assert.Equal(id,first.Data.IntakeRequestId);
        request.PartitionKey="foreign";await Assert.ThrowsAsync<ArgumentException>(()=>f.Bridge.EnsureAsync(request));
    }
    [Fact] public async Task ForeignCustomerCannotBeLinked()
    {
        var f=new Fixture(); var input=Input();input.CustomerId=Guid.NewGuid();
        f.Customers.Setup(x=>x.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new Customer{PartitionKey="foreign"});
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.CreateAsync(input));Assert.Empty(f.Store);
    }
    [Fact] public async Task WonKeepsAttributionAndRequiresConfiguredReason()
    {
        var f=new Fixture(); var input=Input();input.Source="Referral";input.ReferralName="Test partner";
        var lead=await f.Service.CreateAsync(input);
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.StageAsync(lead.Id,new(){ExpectedVersion=lead.Version,Stage="WON",Reason="unknown"}));
        lead=await f.Service.StageAsync(lead.Id,new(){ExpectedVersion=lead.Version,Stage="WON",Reason="Accepted proposal"});
        Assert.Equal("Referral",lead.Source);Assert.Equal("Test partner",lead.ReferralName);Assert.Equal(2,lead.Activity.Count);Assert.NotNull(lead.WonAtUtc);
    }
    [Fact] public async Task EstimateHandoffRequiresIndependentPermissionAndLinksExistingCustomer()
    {
        var f=new Fixture();var customer=Guid.NewGuid();var entity=Entity(Guid.NewGuid());entity.Data.CustomerId=customer;f.Store[entity.Id]=entity;
        f.Access.Setup(x=>x.RequirePermissionAsync(TurnKeyPermissionKeys.EstimatesWrite,It.IsAny<CancellationToken>())).ThrowsAsync(new MedInsights.Lib.ForbiddenAccessException("Denied"));
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Service.EstimateAsync(entity.Id,"v1"));
        f.Estimates.Verify(x=>x.PrepareFromLeadAsync(It.IsAny<LeadDto>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);
        f.Access.Reset();
        var result=await f.Service.EstimateAsync(entity.Id,"v1");
        Assert.Equal(entity.Id,result.EstimateId);Assert.Equal("ESTIMATING",result.Stage);
        f.Estimates.Verify(x=>x.PrepareFromLeadAsync(It.Is<LeadDto>(e=>e.Id==entity.Id && e.CustomerId==customer),entity.Id,It.IsAny<CancellationToken>()),Times.Once);
        Assert.Single(f.Store);
    }
    [Fact] public void TradeDefaultsAndTenantOverridesAreIndependent()
    {
        var lead=new LeadDto{TradeProfile="land-clearing"};
        Assert.Contains(LeadConfigurationService.Fields(lead,new()),x=>x.Key=="acreage");
        var config=new LeadConfigurationDto{RequiredFields=new(){["land-clearing"]=new(){["permit"]="Permit status"}}};
        Assert.Contains(LeadConfigurationService.Fields(lead,config),x=>x.Key=="permit");
        Assert.DoesNotContain(LeadConfigurationService.Fields(lead,config),x=>x.Key=="acreage");
    }
    [Fact] public async Task LongActivityLivesOutsideTheTableEnvelope()
    {
        var f = new Fixture();var lead = await f.Service.CreateAsync(Input());
        for (var i = 0; i < 20; i++) lead = await f.Service.NoteAsync(lead.Id, new() { ExpectedVersion = lead.Version, Text = new string('x', 4000) });
        Assert.Equal(21, (await f.Service.GetAsync(lead.Id))!.Activity.Count);
        Assert.True(f.MaxTableBytes < 24000);
        Assert.NotNull(f.Store[lead.Id].ActivityBlobName);
    }

    [Fact] public async Task ExplicitCustomerCreationStaysInTheLeadWorkspace()
    {
        var f = new Fixture();var lead = await f.Service.CreateAsync(Input());
        var linked = await f.Service.CreateCustomerAsync(lead.Id, lead.Version);
        Assert.Equal(lead.Id, linked.CustomerId);
        f.Customers.Verify(x => x.SaveAsync(It.Is<Customer>(c => c.Id == lead.Id && c.PartitionKey == Pk), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact] public async Task RetryingTheSameVisitDoesNotCreateAnotherCalendarEntry()
    {
        var f = new Fixture(); var lead = await f.Service.CreateAsync(Input());
        CalendarEventDto? saved = null;
        f.Calendar.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(() => saved);
        f.Calendar.Setup(x => x.AddAsync(It.IsAny<CalendarEventDto>())).ReturnsAsync((CalendarEventDto dto) => saved = dto);
        var input = new LeadScheduleDto { ExpectedVersion = lead.Version, StartUtc = DateTime.UtcNow.AddDays(1), EndUtc = DateTime.UtcNow.AddDays(1).AddHours(1) };
        var scheduled = await f.Service.ScheduleAsync(lead.Id, input);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ScheduleAsync(lead.Id, input));
        input.ExpectedVersion = scheduled.Version;
        await f.Service.ScheduleAsync(lead.Id, input);
        f.Calendar.Verify(x => x.AddAsync(It.IsAny<CalendarEventDto>()), Times.Once);
        Assert.Equal(lead.Id, saved!.LeadId);
    }

    private static CreateLeadDto Input()=>new(){Title="Test opportunity",ContactName="Test contact",Email="lead@example.invalid",SiteAddress="Test site",RequestedWork="Repair scope"};
    private static Lead Entity(Guid id)=>new(){Id=id,PartitionKey=Pk,RowKey=RepositoryKeyHelper.ToRowKey(id),Data=new(){Id=id,TenantId=Tenant,Title="Test opportunity",Email="lead@example.invalid",SiteAddress="Test site",RequestedWork="Repair scope",Version="v1"}};
    private sealed class User:IUserContext {public bool IsAuthenticated=>true;public Guid TenantId=>Tenant;public Guid UserId=>Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");public AppTimeZone Timezone=>AppTimeZone.Utc;public string FirstName=>"Test";public string LastName=>"User";}
    private sealed class Fixture
    {
        public Dictionary<Guid,Lead> Store {get;}=[];
        public int MaxTableBytes {get;private set;}
        public Mock<ILeadRepository> Repository {get;}=new();
        public Mock<ICustomerRepository> Customers {get;}=new();
        public Mock<IRoleAccessService> Access {get;}=new();
        public Mock<IQuoteEstimateService> Estimates {get;}=new();
        public Mock<ICalendarEventService> Calendar {get;}=new();
        public LeadConfigurationDto Config {get;set;}=new();
        public LeadIntakeBridge Bridge {get;}
        public LeadService Service {get;}
        public Fixture()
        {
            Repository.Setup(x=>x.ListAsync(Pk,It.IsAny<CancellationToken>())).ReturnsAsync(()=>Store.Values.ToArray());
            Repository.Setup(x=>x.GetAsync(Pk,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string p,string r,CancellationToken ct)=>Store.Values.FirstOrDefault(x=>x.RowKey==r));
            Repository.Setup(x=>x.CommitAsync(It.IsAny<Lead>(),It.IsAny<bool>(),It.IsAny<CancellationToken>())).ReturnsAsync((Lead lead,bool create,CancellationToken ct)=>{MaxTableBytes=Math.Max(MaxTableBytes,JsonSerializer.SerializeToUtf8Bytes(lead).Length);Store[lead.Id]=lead;return lead;});
            Customers.Setup(x=>x.ListAsync(Pk,It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Customer>());
            var settings=new Mock<ITenantSettingsRepository>();
            settings.Setup(x=>x.GetAsync(Pk,"SETTINGS|OPERATIONAL",It.IsAny<CancellationToken>(),false)).ReturnsAsync(()=>new TenantSettingsDocument{ValuesJson=JsonSerializer.Serialize(new{leads=Config},new JsonSerializerOptions(JsonSerializerDefaults.Web))});
            var configuration=new LeadConfigurationService(settings.Object);Bridge=new(Repository.Object,configuration);
            var members=new Mock<MedInsights.Repositories.Interfaces.ITenantMembershipRepository>();members.Setup(x=>x.GetActiveAssignedByTenantAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<MedInsights.Lib.Entities.TenantMembership>());
            Estimates.Setup(x=>x.PrepareFromLeadAsync(It.IsAny<LeadDto>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync((LeadDto dto,Guid requestId,CancellationToken ct)=>new QuoteEstimateDto{Id=requestId,LeadId=dto.Id,CustomerId=dto.CustomerId});
            var blobContents = new Dictionary<string, byte[]>();
            var blobs = new Mock<MedInsights.AzureServices.Interfaces.IAzureBlobStorageService>();
            blobs.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string,string>>(), It.IsAny<CancellationToken>()))
                .Returns((string container, string name, Stream stream, string type, IReadOnlyDictionary<string,string> metadata, CancellationToken ct) => { using var memory = new MemoryStream(); stream.CopyTo(memory); blobContents[name] = memory.ToArray(); return Task.CompletedTask; });
            blobs.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string container, string name, CancellationToken ct) => (Stream)new MemoryStream(blobContents[name]));
            Service=new(Repository.Object,Mock.Of<IQuoteRequestRepository>(),Customers.Object,members.Object,Mock.Of<IJobRepository>(),new User(),Access.Object,Mock.Of<IAuditService>(),configuration,Bridge,Calendar.Object,Estimates.Object,Mock.Of<IJobService>(),Mock.Of<IQuoteRequestAttachmentService>(),Mock.Of<IBeam.Communications.Abstractions.IEmailService>(),Mock.Of<IBeam.Communications.Abstractions.ISmsService>(),Mock.Of<ITenantCommunicationProfileResolver>(),new LeadActivityStore(blobs.Object,Repository.Object));
        }
    }
}
