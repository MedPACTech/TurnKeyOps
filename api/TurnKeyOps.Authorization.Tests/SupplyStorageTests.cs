using System.Collections.Concurrent;
using Azure;
using Azure.Data.Tables;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.Authorization.Tests;
public sealed class SupplyStorageTests
{
    [AzuriteFact][Trait("Category","StorageIntegration")]
    public async Task ConcurrentReservationsCommitOnceAndTenantPointersRemainIsolated()
    {
        var blobs=new ConcurrentDictionary<string,byte[]>();var mock=new Mock<IAzureBlobStorageService>();
        mock.Setup(b=>b.UploadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<Stream>(),It.IsAny<string>(),It.IsAny<IReadOnlyDictionary<string,string>>(),It.IsAny<CancellationToken>()))
            .Returns(async(string container,string name,Stream stream,string type,IReadOnlyDictionary<string,string>? meta,CancellationToken ct)=>{using var memory=new MemoryStream();await stream.CopyToAsync(memory,ct);blobs[name]=memory.ToArray();});
        mock.Setup(b=>b.OpenReadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string container,string name,CancellationToken ct)=>(Stream)new MemoryStream(blobs[name]));
        mock.Setup(b=>b.DeleteIfExistsAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).Returns((string container,string name,CancellationToken ct)=>{blobs.TryRemove(name,out _);return Task.CompletedTask;});
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var tenant=Guid.NewGuid();var store=new SupplyStore(config,mock.Object);var w=new InventoryLocation{Name="Test"};var d=new MaterialDemand{Strategy="stock",ItemId="item",Quantity=8};var original=new SupplyState{Catalog=[new(){Id="item",Name="Item"}],Locations=[w],Demands=[d]};
        SupplyRules.Move(original,new(){Action="adjust",State="add",ItemId="item",LocationId=w.Id,Quantity=10,Reason="Test count"},"test",DateTime.UtcNow);
        try{
            await store.SaveAsync(tenant,original,"");Assert.Empty((await store.ReadAsync(Guid.NewGuid())).Catalog);
            var a=await store.ReadAsync(tenant);var b=await store.ReadAsync(tenant);
            SupplyRules.Reserve(a,new(){TargetId=d.Id,LocationId=w.Id,Quantity=8},"a",DateTime.UtcNow);SupplyRules.Reserve(b,new(){TargetId=d.Id,LocationId=w.Id,Quantity=8},"b",DateTime.UtcNow);
            async Task<bool> Save(SupplyState s){try{await store.SaveAsync(tenant,s,s.Version);return true;}catch(InvalidOperationException){return false;}}
            var results=await Task.WhenAll(Save(a),Save(b));Assert.Single(results,result=>result);
            var final=await store.ReadAsync(tenant);Assert.Single(final.Reservations);Assert.Equal(2,SupplyRules.Balance(final,"item",w.Id,DateTime.UtcNow).Available);
            Assert.NotNull(final.PreviousBlob);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(tenant,original,""));
        }finally{await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("SupplyVersions").DeleteEntityAsync(tenant.ToString("N"),"current",ETag.All);}
    }
}
