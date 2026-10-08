using System.Collections.Concurrent;
using Azure;
using Azure.Data.Tables;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
namespace TurnKeyOps.Authorization.Tests;
public sealed class FinanceStorageTests
{
    [AzuriteFact][Trait("Category","StorageIntegration")]
    public async Task FinanceCommitIsAtomicImmutableAndTenantIsolatedUnderConcurrentPostingAndClose()
    {
        var content=new ConcurrentDictionary<string,byte[]>();var blobs=new Mock<IAzureBlobStorageService>();
        blobs.Setup(b=>b.UploadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<Stream>(),It.IsAny<string>(),It.IsAny<IReadOnlyDictionary<string,string>>(),It.IsAny<CancellationToken>())).Returns(async(string container,string name,Stream stream,string type,IReadOnlyDictionary<string,string>? metadata,CancellationToken ct)=>{Assert.Equal(FinanceStore.Container,container);using var buffer=new MemoryStream();await stream.CopyToAsync(buffer,ct);content[name]=buffer.ToArray();});
        blobs.Setup(b=>b.OpenReadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string container,string name,CancellationToken ct)=>(Stream)new MemoryStream(content[name]));
        blobs.Setup(b=>b.DeleteIfExistsAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((string container,string name,CancellationToken ct)=>{content.TryRemove(name,out _);return Task.CompletedTask;});
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var store=new FinanceStore(configuration,blobs.Object);var tenant=Guid.NewGuid();
        try
        {
            await store.SaveAsync(tenant,FinanceTests.State(),"");Assert.Empty((await store.ReadAsync(Guid.NewGuid())).Accounts);
            var a=await store.ReadAsync(tenant);var b=await store.ReadAsync(tenant);
            FinanceLedger.Post(a,new(2026,9,15),"manual","cash","Opening cash",[new("1000",100,0),new("3000",0,100)],"owner");b.Periods[0].Status="CLOSED";
            async Task<bool> Save(FinanceState state){try{await store.SaveAsync(tenant,state,state.Version);return true;}catch(InvalidOperationException){return false;}}
            var results=await Task.WhenAll(Save(a),Save(b));Assert.Single(results,v=>v);
            var final=await store.ReadAsync(tenant);Assert.True(final.Journals.Count==1&&final.Periods[0].Status=="OPEN"||final.Journals.Count==0&&final.Periods[0].Status=="CLOSED");
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(tenant,a,a.Version));
            if(final.Journals.Count>0){final.Journals.Clear();await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(tenant,final,final.Version));}
            Assert.All(content.Keys,key=>Assert.StartsWith(tenant.ToString("N")+"/",key));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.ReadAsync(Guid.Empty));
        }
        finally{await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("FinanceVersions").DeleteEntityAsync(tenant.ToString("N"),"current",ETag.All);}
    }
}
