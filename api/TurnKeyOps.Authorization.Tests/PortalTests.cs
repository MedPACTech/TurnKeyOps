using System.Text.Json;
using Azure;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Enums;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace MedInsights.Authorization.Tests;

public sealed class PortalTests
{
    private static readonly Guid Tenant=Guid.NewGuid(),User=Guid.NewGuid(),Customer=Guid.NewGuid();
    private static string P=>RepositoryKeyHelper.ToTenantPartitionKey(Tenant);
    private static PortalActor Actor(string scope="customer",Guid? record=null)=>new(Tenant,User,[new(){UserId=User,CustomerId=Customer,Scope=scope,RecordId=record??Customer}],new(){Enabled=true});
    [Fact]public void GrantsDoNotPermitOtherCustomersOrWrongScope()
    {
        var site=Guid.NewGuid();var a=Actor("site",site);
        Assert.True(PortalAccessService.Allows(a,"job",Guid.NewGuid(),Customer,site));
        Assert.False(PortalAccessService.Allows(a,"job",Guid.NewGuid(),Guid.NewGuid(),site));
        Assert.False(PortalAccessService.Allows(a,"job",Guid.NewGuid(),Customer,Guid.NewGuid()));
        Assert.False(PortalAccessService.Allows(a,"lead",Guid.NewGuid(),Customer));
        Assert.False(PortalAccessService.Allows(a,"job",Guid.NewGuid(),null,site));
    }
    [Theory][InlineData(true,false)][InlineData(false,true)]
    public void RevokedAndExpiredGrantsFail(bool revoked,bool expired)
    {var a=Actor();a.Grants[0].Revoked=revoked;a.Grants[0].ExpiresAtUtc=expired?DateTime.UtcNow.AddSeconds(-1):DateTime.UtcNow.AddHours(1);Assert.False(PortalAccessService.Allows(a,"job",Guid.NewGuid(),Customer));}
    [Fact]public void MultipleContactsCanHoldIndependentScopedGrants()
    {var a=Actor();var b=Actor() with {UserId=Guid.NewGuid()};Assert.False(PortalAccessService.Allows(b,"job",Guid.NewGuid(),Customer));b.Grants[0].UserId=b.UserId;Assert.True(PortalAccessService.Allows(a,"job",Guid.NewGuid(),Customer));Assert.True(PortalAccessService.Allows(b,"job",Guid.NewGuid(),Customer));}
    private sealed class AccessFixture
    {
        public PortalAccessState State=new(){Configuration=new(){Enabled=true},Grants=[new(){UserId=User,CustomerId=Customer,Scope="customer",RecordId=Customer}]};
        public UserProfile Profile=new(){ApplicationUserId=User,PartitionKey=P,CustomerId=Customer,IsActive=true,ProfileTypes=["Customer"],ModulePermissions=[]};
        public PortalAccessService Service;
        public AccessFixture(){var store=new Mock<IPortalAccessStore>();store.Setup(s=>s.ReadAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>State);store.Setup(s=>s.ReadAsync(It.Is<Guid>(t=>t!=Tenant),It.IsAny<CancellationToken>())).ReturnsAsync(new PortalAccessState());
            var profiles=new Mock<IManagedProfileStore>();profiles.Setup(p=>p.GetByKeysAsync(P,RepositoryKeyHelper.ToRowKey(User),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Profile);
            Service=new(store.Object,profiles.Object,Mock.Of<IAuditService>());}
        public async Task<string> Login(){var result=JsonSerializer.SerializeToElement(await Service.ActivateAsync(Tenant,User,default),JobConfigurationService.Json);return result.GetProperty("token").GetString()!;}
    }
    [Fact]public async Task DisablingContactRevokesEveryGrantAndSessionWithoutAffectingOtherContacts()
    {
        var f=new AccessFixture();f.State.Version="v1";var token=await f.Login();var other=Guid.NewGuid();
        f.State.Grants.Add(new(){UserId=User,CustomerId=Customer,Scope="job",RecordId=Guid.NewGuid()});
        f.State.Grants.Add(new(){UserId=other,CustomerId=Customer});
        f.State.Sessions.Add(new(){UserId=other,TokenHash="other"});
        await f.Service.RevokeContactAsync(Tenant,Guid.NewGuid(),User,f.State.Version,default);
        Assert.All(f.State.Grants.Where(g=>g.UserId==User),g=>Assert.True(g.Revoked));
        Assert.All(f.State.Sessions.Where(g=>g.UserId==User),g=>Assert.True(g.Revoked));
        Assert.False(f.State.Grants.Single(g=>g.UserId==other).Revoked);
        Assert.False(f.State.Sessions.Single(g=>g.UserId==other).Revoked);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.AuthenticateAsync(Tenant,token,default));
    }
    [Fact]public async Task StaleContactRevocationDoesNotMutateAnyGrant()
    {
        var f=new AccessFixture();f.State.Version="current";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.RevokeContactAsync(Tenant,Guid.NewGuid(),User,"stale",default));
        Assert.All(f.State.Grants,g=>Assert.False(g.Revoked));
    }
    [Fact]public async Task IdentityWithoutEmployeePermissionsActivatesOnlyGrantedCustomer()
    {var f=new AccessFixture();var token=await f.Login();Assert.Equal(64,token.Length);Assert.DoesNotContain(f.State.Sessions,s=>s.TokenHash==token);Assert.Equal(User,(await f.Service.AuthenticateAsync(Tenant,token,default)).UserId);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.ActivateAsync(Tenant,Guid.NewGuid(),default));}
    [Fact]public async Task SessionIsTenantBoundAndRandomBearerCannotGrantAccess()
    {var f=new AccessFixture();var token=await f.Login();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.AuthenticateAsync(Guid.NewGuid(),token,default));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.AuthenticateAsync(Tenant,new string('A',64),default));}
    [Theory][InlineData("expired")][InlineData("revoked")][InlineData("removed")][InlineData("unlinked")][InlineData("disabled")][InlineData("grant")]
    public async Task EveryRequestRevalidatesSessionAndCurrentContact(string change)
    {var f=new AccessFixture();var token=await f.Login();switch(change){case "expired":f.State.Sessions[0].ExpiresAtUtc=DateTime.UtcNow.AddSeconds(-1);break;case "revoked":await f.Service.LogoutAsync(Tenant,token,default);break;case "removed":f.Profile.IsDeleted=true;break;case "unlinked":f.Profile.CustomerId=Guid.NewGuid();break;case "disabled":f.State.Configuration.Enabled=false;break;case "grant":f.State.Grants[0].Revoked=true;break;}await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Service.AuthenticateAsync(Tenant,token,default));}
    [Fact]public void InternalRolesCannotSubstituteForCustomerRelationship()
    {var p=new UserProfile{ApplicationUserId=User,PartitionKey=P,IsActive=true,Role="owner",ModulePermissions=["jobs.read","estimates.read"]};Assert.False(PortalAccessService.ProfileAllows(p,Tenant,User,Customer));}
    private sealed class Fixture
    {
        public Job Job=new(){Id=Guid.NewGuid(),PartitionKey=P,CustomerId=Customer,JobSiteId=Guid.NewGuid(),Name="Approved work",Notes="PRIVATE MARGIN",Status=JobStatus.CompletionReview,ETag=new ETag("v1")};
        public JobWorkflowPayloadDto Payload=new(){Execution=new(){SoldScope="Approved scope",Tasks=[],Issues=[new(){Description="PRIVATE BLOCKER",Severity="info"}],Evidence=[new(){Name="PRIVATE FILE"}],Profile=new(){CustomerAcceptanceRequired=true}},Activity=[new(){Label="PRIVATE STAFF NOTE"},new(){Label="Visible progress",CustomerVisible=true}]};
        public Mock<IJobRepository> Jobs=new();public Mock<ICalendarEventRepository> Calendar=new();public Mock<IAzureBlobStorageService> Blobs=new();public PortalService Service;public PortalActor Actor=PortalTests.Actor();
        public Fixture(){Jobs.Setup(r=>r.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Job);Jobs.Setup(r=>r.SaveAsync(It.IsAny<Job>(),It.IsAny<CancellationToken>())).Callback(()=>Job.ETag=new ETag("v2")).ReturnsAsync(()=>Job);
            var payloads=new Mock<IJobWorkflowPayloadStore>();payloads.Setup(p=>p.LoadAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Payload);payloads.Setup(p=>p.SaveAsync(Tenant,Job.Id,It.IsAny<JobWorkflowPayloadDto>(),It.IsAny<CancellationToken>())).ReturnsAsync($"{Tenant:N}/{Job.Id:N}/payload");
            var access=new PortalAccessService(Mock.Of<IPortalAccessStore>(),Mock.Of<IManagedProfileStore>(),Mock.Of<IAuditService>());
            Service=new(Jobs.Object,Mock.Of<ILeadRepository>(),Calendar.Object,Mock.Of<IQuoteEstimateRepository>(),null!,payloads.Object,access,Blobs.Object);}
        public Task<object> Command(string action,string? hash=null)=>Service.JobActionAsync(Actor,Job.Id,new(){Action=action,ExpectedVersion=Job.ETag.ToString(),Hash=hash??"",Consent=true,Signer="Customer",Text="Service issue"},default);
    }
    [Fact]public async Task JobProjectionAndCustomerBobNeverContainPrivateFields()
    {var f=new Fixture();foreach(var result in new[]{await f.Service.WorkAsync(f.Actor,"job",f.Job.Id,default),await f.Service.ExplainAsync(f.Actor,"job",f.Job.Id,default)}){var json=JsonSerializer.Serialize(result);Assert.DoesNotContain("PRIVATE",json);Assert.Contains("Visible progress",json);Assert.DoesNotContain("MembershipIds",json);}}
    [Theory][InlineData("customer")][InlineData("tenant")][InlineData("deleted")]
    public async Task GuessedJobIdsAndForeignTenantsFailBeforePayloadReads(string mismatch)
    {var f=new Fixture();if(mismatch=="customer")f.Job.CustomerId=Guid.NewGuid();if(mismatch=="tenant")f.Job.PartitionKey="other";if(mismatch=="deleted")f.Job.IsDeleted=true;
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.WorkAsync(f.Actor,"job",f.Job.Id,default));await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Command("report-issue"));f.Jobs.Verify(j=>j.SaveAsync(It.IsAny<Job>(),It.IsAny<CancellationToken>()),Times.Never);}
    [Fact]public async Task FileVisibilityAndTenantPathAreEnforcedBeforeBlobRead()
    {var f=new Fixture();var file=f.Payload.Execution!.Evidence[0];await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.JobFileAsync(f.Actor,f.Job.Id,file.Id,default));file.CustomerVisible=true;file.BlobName="other-tenant/file";await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.JobFileAsync(f.Actor,f.Job.Id,file.Id,default));f.Blobs.Verify(b=>b.OpenReadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);}
    [Fact]public async Task CompletionUsesExactEvidenceHashAndSeparateAcceptance()
    {var f=new Fixture();var hash=PortalRules.CompletionHash(f.Payload.Execution!);await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.JobActionAsync(f.Actor,f.Job.Id,new(){Action="accept-completion",ExpectedVersion="v1",Hash="stale",Consent=true,Signer="Customer"},default));
        await f.Service.JobActionAsync(f.Actor,f.Job.Id,new(){Action="accept-completion",ExpectedVersion="v1",Hash=hash,Consent=true,Signer="Customer"},default);var a=Assert.Single(f.Payload.Execution!.Acceptances);Assert.Equal(hash,a.CompletionHash);Assert.Equal($"portal:{User:D}",a.RecordedBy);Assert.Null(f.Payload.AcceptedEstimate);}
    [Fact]public async Task ServiceIssueIsStructuredAndInvalidatesCurrentAcceptance()
    {var f=new Fixture();f.Payload.Execution!.Acceptances.Add(new(){CompletionRevision=1});await f.Command("report-issue");var issue=f.Payload.Execution.Issues.Last();Assert.Equal(User,issue.PortalUserId);Assert.Equal(f.Job.Id,issue.OriginalJobId);Assert.Equal(2,f.Payload.Execution.CompletionRevision);}
    [Fact]public async Task StaleVersionCannotSubmitCustomerAction()
    {var f=new Fixture();await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.JobActionAsync(f.Actor,f.Job.Id,new(){Action="report-issue",ExpectedVersion="stale",Text="Issue"},default));f.Jobs.Verify(j=>j.SaveAsync(It.IsAny<Job>(),It.IsAny<CancellationToken>()),Times.Never);}
    [Fact]public async Task ChangeApprovalRejectsWrongRevisionAndUnsignedPricing()
    {var f=new Fixture();var c=new JobChangeDto{CustomerVisible=true,Status="APPROVAL_REQUIRED",PricingImpact=true,Description="Extra work"};f.Payload.Execution!.Changes.Add(c);
        foreach(var hash in new[]{"stale",PortalRules.ChangeHash(c)})await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.JobActionAsync(f.Actor,f.Job.Id,new(){Action="approve-change",ExpectedVersion="v1",Hash=hash,ItemId=c.Id,Consent=true},default));Assert.Equal("APPROVAL_REQUIRED",c.Status);}
    [Fact]public async Task UnpricedChangeRecordsExactDecisionAndIdentity()
    {var f=new Fixture();var c=new JobChangeDto{CustomerVisible=true,Status="APPROVAL_REQUIRED",Description="Move location"};f.Payload.Execution!.Changes.Add(c);var hash=PortalRules.ChangeHash(c);await f.Service.JobActionAsync(f.Actor,f.Job.Id,new(){Action="approve-change",ExpectedVersion="v1",Hash=hash,ItemId=c.Id,Consent=true},default);Assert.Equal("APPROVED",c.Status);Assert.Equal(hash,c.CustomerDecisionHash);Assert.Equal(User,c.PortalUserId);}
    [Fact]public async Task AppointmentShowsNoStaffDetailsAndUsesSharedRepositoryWithVersion()
    {var f=new Fixture();var slot=new PortalSlot(Guid.NewGuid(),DateTime.UtcNow.AddDays(2),DateTime.UtcNow.AddDays(2).AddHours(1));var e=new CalendarEvent{Id=Guid.NewGuid(),JobId=f.Job.Id,PartitionKey=P,CustomerVisible=true,ETag=new ETag("v1"),StartUtc=DateTime.UtcNow.AddDays(1),EndUtc=DateTime.UtcNow.AddDays(1).AddHours(1),MembershipIds=[Guid.NewGuid()],Description="PRIVATE",CustomerSlots=[slot]};
        f.Calendar.Setup(r=>r.GetAsync(P,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(e);f.Calendar.Setup(r=>r.SaveAsync(e,It.IsAny<CancellationToken>())).ReturnsAsync(e);f.Actor.Configuration.SelfBookingEnabled=true;
        var result=await f.Service.AppointmentAsync(f.Actor,e.Id,new(){ExpectedVersion="v1",Action="confirmed",SlotId=slot.Id},default);Assert.Equal(slot.StartUtc,e.StartUtc);Assert.DoesNotContain("PRIVATE",JsonSerializer.Serialize(result));Assert.DoesNotContain("Membership",JsonSerializer.Serialize(result));f.Calendar.Verify(r=>r.SaveAsync(e,It.IsAny<CancellationToken>()),Times.Once);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.AppointmentAsync(f.Actor,e.Id,new(){ExpectedVersion="stale",Action="confirmed"},default));}
    [Theory][InlineData("BLOCKED","Waiting on next step")][InlineData("READY_TO_SCHEDULE","Preparing your project")][InlineData("COMPLETION_REVIEW","Finalizing your project")]
    public void StatusMappingDoesNotExposeInternalTerminology(string state,string expected)=>Assert.Equal(expected,PortalRules.JobStatus(state));
}
