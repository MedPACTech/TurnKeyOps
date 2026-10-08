using System.Collections.Concurrent;
using Azure;
using Azure.Data.Tables;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.Authorization.Tests;
public sealed class PortalStorageTests
{
    [AzuriteFact][Trait("Category","StorageIntegration")]
    public async Task AccessMetadataIsTenantIsolatedAndRevocationCannotBeOverwrittenByStaleSessionCreation()
    {
        var blobs=new ConcurrentDictionary<string,byte[]>();var mock=new Mock<IAzureBlobStorageService>();
        mock.Setup(b=>b.UploadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<Stream>(),It.IsAny<string>(),It.IsAny<IReadOnlyDictionary<string,string>>(),It.IsAny<CancellationToken>()))
            .Returns(async(string container,string name,Stream stream,string type,IReadOnlyDictionary<string,string>? meta,CancellationToken ct)=>{using var memory=new MemoryStream();await stream.CopyToAsync(memory,ct);blobs[name]=memory.ToArray();});
        mock.Setup(b=>b.OpenReadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string container,string name,CancellationToken ct)=>(Stream)new MemoryStream(blobs[name]));
        mock.Setup(b=>b.DeleteIfExistsAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((string container,string name,CancellationToken ct)=>{blobs.TryRemove(name,out _);return Task.CompletedTask;});
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var tenant=Guid.NewGuid();var store=new PortalAccessStore(config,mock.Object);
        try{
            await store.SaveAsync(tenant,new(){Configuration=new(){Enabled=true},Grants=[new(){UserId=Guid.NewGuid(),CustomerId=Guid.NewGuid()}]},"");
            Assert.Empty((await store.ReadAsync(Guid.NewGuid())).Grants);
            var revoke=await store.ReadAsync(tenant);var stale=await store.ReadAsync(tenant);revoke.Grants[0].Revoked=true;await store.SaveAsync(tenant,revoke,revoke.Version);
            stale.Sessions.Add(new(){TokenHash="test",ExpiresAtUtc=DateTime.UtcNow.AddHours(1)});
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(tenant,stale,stale.Version));
            var final=await store.ReadAsync(tenant);Assert.True(final.Grants[0].Revoked);Assert.Empty(final.Sessions);
        }finally{await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("PortalAccessVersions").DeleteEntityAsync(tenant.ToString("N"),"current",ETag.All);}
    }
}
