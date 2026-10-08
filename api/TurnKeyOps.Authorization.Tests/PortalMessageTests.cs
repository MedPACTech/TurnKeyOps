using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using MedInsights.AzureServices.Interfaces;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
namespace MedInsights.Authorization.Tests;
public sealed class PortalMessageTests
{
    private sealed class Fixture
    {
        public Guid Tenant=Guid.NewGuid(),User=Guid.NewGuid(),Customer=Guid.NewGuid(),LeadId=Guid.NewGuid();
        public Mock<IChatMessageRepository> Messages=new();public Mock<IAzureBlobStorageService> Blobs=new();public List<ChatMessage> Rows=[];public PortalActor Actor;public PortalMessages Service;
        public Fixture(){Actor=new(Tenant,User,[new(){UserId=User,CustomerId=Customer,RecordId=Customer}],new(){Enabled=true});
            var leads=new Mock<ILeadRepository>();leads.Setup(l=>l.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(Tenant),RepositoryKeyHelper.ToRowKey(LeadId),It.IsAny<CancellationToken>())).ReturnsAsync(new Lead{Id=LeadId,PartitionKey=RepositoryKeyHelper.ToTenantPartitionKey(Tenant),Data=new(){CustomerId=Customer}});
            var access=new PortalAccessService(Mock.Of<IPortalAccessStore>(),Mock.Of<IManagedProfileStore>(),Mock.Of<IAuditService>());
            var portal=new PortalService(null!,leads.Object,null!,null!,null!,null!,access,Blobs.Object);
            Messages.Setup(m=>m.GetMessagesByChatAsync(It.IsAny<string>(),It.IsAny<Guid>(),200,It.IsAny<CancellationToken>())).ReturnsAsync(()=>Rows);
            Messages.Setup(m=>m.AppendCustomerMessageAsync(It.IsAny<ChatMessage>(),It.IsAny<CancellationToken>())).Callback((ChatMessage m,CancellationToken _)=>Rows.Add(m)).ReturnsAsync((ChatMessage m,CancellationToken _)=>m);
            Service=new(Messages.Object,portal,Blobs.Object,access);
        }
    }
    [Fact]public async Task MessagesRetainContextAndExcludeInternalAndOtherCustomerRows()
    {
        var f=new Fixture();await f.Service.SendAsync(f.Actor,"lead",f.LeadId,"Customer question",default);
        var visible=Assert.Single(f.Rows);Assert.Contains("|PORTAL",visible.PartitionKey);Assert.Equal(f.User,visible.ActorUserId);
        var hidden=JsonSerializer.Deserialize<ChatMessage>(JsonSerializer.Serialize(visible))!;hidden.Id=Guid.NewGuid();hidden.Content="PRIVATE MESSAGE";hidden.MetadataJson="{}";f.Rows.Add(hidden);
        var foreign=JsonSerializer.Deserialize<ChatMessage>(JsonSerializer.Serialize(visible))!;foreign.Id=Guid.NewGuid();foreign.Content="OTHER TENANT";foreign.TenantId=Guid.NewGuid();f.Rows.Add(foreign);
        var output=JsonSerializer.Serialize(await f.Service.ListAsync(f.Actor,"lead",f.LeadId,default));Assert.Contains("Customer question",output);Assert.DoesNotContain("PRIVATE",output);Assert.DoesNotContain("OTHER TENANT",output);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.ListAsync(f.Actor,"lead",Guid.NewGuid(),default));
    }
    [Fact]public async Task UploadChecksEntitlementTypeSizeAndWritesOnlyPrivateScopedMetadata()
    {
        var f=new Fixture();await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.UploadAsync(f.Actor,"lead",f.LeadId,"photo.jpg","image/jpeg",new MemoryStream(Encoding.UTF8.GetBytes("<script>")),default));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.UploadAsync(f.Actor,"lead",Guid.NewGuid(),"photo.jpg","image/jpeg",new MemoryStream([255,216,255,217]),default));
        f.Blobs.Verify(b=>b.UploadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<Stream>(),It.IsAny<string>(),It.IsAny<IReadOnlyDictionary<string,string>>(),It.IsAny<CancellationToken>()),Times.Never);
        await f.Service.UploadAsync(f.Actor,"lead",f.LeadId,"photo.jpg","image/jpeg",new MemoryStream([255,216,255,217]),default);
        f.Blobs.Verify(b=>b.UploadAsync("portal-files",It.Is<string>(p=>p.StartsWith($"{f.Tenant:N}/lead/{f.LeadId:N}/")),It.IsAny<Stream>(),"image/jpeg",It.IsAny<IReadOnlyDictionary<string,string>>(),It.IsAny<CancellationToken>()),Times.Once);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Service.DownloadAsync(f.Actor,"lead",f.LeadId,Guid.NewGuid(),default));
    }
}
