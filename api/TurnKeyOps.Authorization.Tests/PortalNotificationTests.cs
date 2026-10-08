using Azure;
using IBeam.Communications.Abstractions;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace MedInsights.Authorization.Tests;
public sealed class PortalNotificationTests
{
    private sealed class Fixture
    {
        public Guid Tenant=Guid.NewGuid(),User=Guid.NewGuid(),Customer=Guid.NewGuid(),JobId=Guid.NewGuid();public PortalAccessState State;public PlatformUser Identity=new(){EmailVerified=true,PrimaryEmail="customer@example.invalid",IsActive=true};public Mock<IEmailService> Email=new();public PortalNotifications Service;
        public Fixture(){var partition=RepositoryKeyHelper.ToTenantPartitionKey(Tenant);State=new(){Configuration=new(){Enabled=true},Grants=[new(){UserId=User,CustomerId=Customer,RecordId=Customer}],NotificationPreferences=new(){[User]=["email"]}};
            var store=new Mock<IPortalAccessStore>();store.Setup(s=>s.ReadAsync(Tenant,It.IsAny<CancellationToken>())).ReturnsAsync(()=>State);
            var profiles=new Mock<IManagedProfileStore>();profiles.Setup(s=>s.GetByKeysAsync(partition,RepositoryKeyHelper.ToRowKey(User),It.IsAny<CancellationToken>())).ReturnsAsync(new UserProfile{ApplicationUserId=User,PartitionKey=partition,CustomerId=Customer,IsActive=true,ProfileTypes=["Customer"]});
            var access=new PortalAccessService(store.Object,profiles.Object,Mock.Of<IAuditService>());
            var jobs=new Mock<IJobRepository>();jobs.Setup(j=>j.GetAsync(partition,RepositoryKeyHelper.ToRowKey(JobId),It.IsAny<CancellationToken>())).ReturnsAsync(new Job{Id=JobId,PartitionKey=partition,CustomerId=Customer,ETag=new ETag("v1")});
            var payloads=new Mock<IJobWorkflowPayloadStore>();payloads.Setup(p=>p.LoadAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new JobWorkflowPayloadDto());
            var portal=new PortalService(jobs.Object,null!,null!,null!,null!,payloads.Object,access,null!);
            var users=new Mock<IPlatformUserRepository>();users.Setup(u=>u.GetAsync($"USER={User:N}","PROFILE",It.IsAny<CancellationToken>(),false)).ReturnsAsync(()=>Identity);
            var receipts=new Mock<IJobNotificationStore>();JobNotification? receipt=null;
            receipts.Setup(r=>r.GetAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>receipt);
            receipts.Setup(r=>r.SaveAsync(It.IsAny<JobNotification>(),It.IsAny<bool>(),It.IsAny<CancellationToken>())).ReturnsAsync((JobNotification r,bool _,CancellationToken _)=>receipt=r);
            var transportProfile=new Mock<ITenantCommunicationProfileResolver>();transportProfile.Setup(p=>p.Resolve(Tenant)).Returns(new TenantCommunicationProfile{EmailFromAddress="office@example.invalid"});
            Service=new(access,portal,users.Object,new JobNotificationDispatcher(receipts.Object),Email.Object,Mock.Of<ISmsService>(),transportProfile.Object);
        }
        public Task<object> Send()=>Service.SendAsync(Tenant,Guid.NewGuid(),User,"job",JobId,"v1","email","new-message",default);
    }
    [Theory][InlineData("opt-out")][InlineData("unverified")][InlineData("revoked")]
    public async Task NotificationRequiresCurrentGrantOptInAndVerifiedContact(string reason)
    {
        var f=new Fixture();if(reason=="opt-out")f.State.NotificationPreferences.Clear();if(reason=="unverified")f.Identity.EmailVerified=false;if(reason=="revoked")f.State.Grants[0].Revoked=true;
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Send());f.Email.Verify(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ProviderAcceptanceAndUncertaintyNeverCauseBlindReplay(bool timeout)
    {
        var f=new Fixture();if(timeout)f.Email.Setup(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        await f.Send();await f.Send();f.Email.Verify(e=>e.SendAsync(It.IsAny<EmailMessage>(),It.IsAny<EmailOptions?>(),It.IsAny<CancellationToken>()),Times.Once);
    }
}
