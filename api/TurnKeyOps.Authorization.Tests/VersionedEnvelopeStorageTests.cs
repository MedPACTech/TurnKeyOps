using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories;
namespace TurnKeyOps.Authorization.Tests;
public sealed class VersionedEnvelopeStorageTests
{
    [AzuriteFact]
    [Trait("Category","StorageIntegration")]
    public async Task PhysicalEnvelopeVersionsSurviveReadsAndRejectConcurrentWrites()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var store=new VersionedEnvelopeStore<QuoteEstimate>(config,"QuoteEstimates");
        var pk="STORAGE-TEST-"+Guid.NewGuid().ToString("N");var rk=Guid.NewGuid().ToString("D");
        var table=new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("QuoteEstimates");
        try
        {
            var created=await store.SaveAsync(new(){Id=Guid.Parse(rk),PartitionKey=pk,RowKey=rk,PayloadBlobName="original"},true,default);
            Assert.False(string.IsNullOrWhiteSpace(created.ETag.ToString()));
            var first=(await store.GetAsync(pk,rk,default))!;var stale=(await store.GetAsync(pk,rk,default))!;
            Assert.Equal(created.ETag,first.ETag);first.PayloadBlobName="updated";
            var updated=await store.SaveAsync(first,false,default);Assert.NotEqual(stale.ETag,updated.ETag);
            var error=await Assert.ThrowsAsync<RequestFailedException>(()=>store.SaveAsync(stale,false,default));Assert.Equal(412,error.Status);
            var read=(await store.GetAsync(pk,rk,default))!;Assert.Equal("updated",read.PayloadBlobName);Assert.Equal(updated.ETag,read.ETag);
            var listed=new List<QuoteEstimate>();await foreach(var row in store.ListAsync(pk,default))listed.Add(row);Assert.Equal(read.ETag,Assert.Single(listed).ETag);
            read.ETag=ETag.All;await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(read,false,default));
        }
        finally{await table.DeleteEntityAsync(pk,rk,ETag.All);}
    }
}
