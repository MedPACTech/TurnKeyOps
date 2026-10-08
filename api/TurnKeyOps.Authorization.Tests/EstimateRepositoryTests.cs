using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories;
using TurnKeyOps.Repositories.Interfaces;
namespace MedInsights.Authorization.Tests;
public sealed class EstimateRepositoryTests
{
    [Fact] public async Task InterfaceDispatchUsesConditionalUpdateAndRejectsStaleVersion()
    {
        var store=new Mock<IAzureTablesRepositoryStore<QuoteEstimate>>();
        IQuoteEstimateRepository repo=new QuoteEstimateRepository(store.Object,new MemoryCache(new MemoryCacheOptions()),Mock.Of<ITenantContext>(),Options.Create(new RepositoryOptions()));
        var packet=new QuoteEstimate{ETag=new ETag("exact-version")};
        store.Setup(x=>x.UpdateAsync(null,packet,TableUpdateMode.Replace,packet.ETag,It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException(412,"Conflict"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repo.SaveAsync(packet));
        store.Verify(x=>x.UpdateAsync(null,packet,TableUpdateMode.Replace,new ETag("exact-version"),It.IsAny<CancellationToken>()),Times.Once);
        store.Verify(x=>x.AddAsync(null,It.IsAny<QuoteEstimate>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task RetriedArchiveReplacesUnsignedSnapshotWithNewerSignedEnvelope()
    {
        var store=new Mock<IAzureTablesRepositoryStore<QuoteEstimate>>();
        var repo=new QuoteEstimateRepository(store.Object,new MemoryCache(new MemoryCacheOptions()),Mock.Of<ITenantContext>(),Options.Create(new RepositoryOptions()));
        var prior=new QuoteEstimate{PartitionKey="tenant",RowKey="estimate|issued|hash",PayloadBlobName="unsigned",DateUpdated=DateTime.UtcNow.AddMinutes(-1),ETag=new ETag("old")};
        store.Setup(x=>x.AddAsync(null,It.IsAny<QuoteEstimate>(),It.IsAny<CancellationToken>())).ThrowsAsync(new RequestFailedException(409,"Exists"));
        store.Setup(x=>x.GetByKeysAsync("tenant","estimate|issued|hash",It.IsAny<CancellationToken>())).ReturnsAsync(prior);
        var current=new QuoteEstimate{PartitionKey="tenant",RowKey="estimate",CustomerAccessTokenHash="hash",PayloadBlobName="signed",DateUpdated=DateTime.UtcNow};
        await repo.ArchiveAsync(current);
        store.Verify(x=>x.UpdateAsync(null,It.Is<QuoteEstimate>(e=>e.PayloadBlobName=="signed"&&e.RowKey==prior.RowKey),TableUpdateMode.Replace,prior.ETag,It.IsAny<CancellationToken>()),Times.Once);
    }
}
