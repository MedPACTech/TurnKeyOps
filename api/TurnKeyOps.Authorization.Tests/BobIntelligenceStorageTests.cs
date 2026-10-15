using System.Collections.Concurrent;
using Azure;
using Azure.Data.Tables;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.Authorization.Tests;
public sealed class BobIntelligenceStorageTests
{
    [AzuriteFact][Trait("Category","StorageIntegration")]
    public async Task BobControlStateRejectsConcurrentClaimsAndCrossTenantReads()
    {
        var content=new ConcurrentDictionary<string,byte[]>();var blobs=new Mock<IAzureBlobStorageService>();
        blobs.Setup(b=>b.UploadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<Stream>(),It.IsAny<string>(),It.IsAny<IReadOnlyDictionary<string,string>>(),It.IsAny<CancellationToken>())).Returns(async(string container,string name,Stream stream,string type,IReadOnlyDictionary<string,string>? metadata,CancellationToken ct)=>{Assert.Equal(BobIntelligenceStore.Container,container);using var buffer=new MemoryStream();await stream.CopyToAsync(buffer,ct);content[name]=buffer.ToArray();});
        blobs.Setup(b=>b.OpenReadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string container,string name,CancellationToken ct)=>(Stream)new MemoryStream(content[name]));
        blobs.Setup(b=>b.DeleteIfExistsAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((string container,string name,CancellationToken ct)=>{content.TryRemove(name,out _);return Task.CompletedTask;});
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var store=new BobIntelligenceStore(configuration,blobs.Object);var tenant=Guid.NewGuid();
        try
        {
            await store.SaveAsync(tenant,new(),"");Assert.Empty((await store.ReadAsync(Guid.NewGuid())).ExecutionClaims);
            var a=await store.ReadAsync(tenant);var b=await store.ReadAsync(tenant);
            a.ExecutionClaims["actor:stable-key"]=Guid.NewGuid();b.ExecutionClaims["actor:stable-key"]=Guid.NewGuid();
            async Task<bool> Save(BobIntelligenceState state){try{await store.SaveAsync(tenant,state,state.Version);return true;}catch(InvalidOperationException){return false;}}
            var results=await Task.WhenAll(Save(a),Save(b));Assert.Single(results,ok=>ok);Assert.Single((await store.ReadAsync(tenant)).ExecutionClaims);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(tenant,a,a.Version));
            await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(tenant,a,"*"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.ReadAsync(Guid.Empty));
            Assert.All(content.Keys,key=>Assert.StartsWith(tenant.ToString("N")+"/",key));
        }
        finally{await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("BobIntelligenceVersions").DeleteEntityAsync(tenant.ToString("N"),"current",ETag.All);}
    }
}
