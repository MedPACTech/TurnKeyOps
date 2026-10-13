using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Data.Tables;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
using Xunit;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories;
using Azure.Storage.Blobs.Specialized;

namespace TurnKeyOps.Authorization.Tests;

public sealed class FinanceSourceStorageTests
{
    [AzuriteFact][Trait("Category", "StorageIntegration")]
    public async Task AbandonedInfiniteLeaseRequiresExplicitAdministrativeBreakBeforeRecovery()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            { ["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true" }).Build();
        var tenant=Guid.NewGuid();var container=new BlobServiceClient("UseDevelopmentStorage=true", new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_12_02)).GetBlobContainerClient(FinanceSourceCoordinator.Container);
        await container.CreateIfNotExistsAsync();var blob=container.GetBlobClient($"{tenant:N}/invoice-source");
        await blob.UploadAsync(BinaryData.FromString("Stopped writer recovery fixture"));
        var abandoned=blob.GetBlobLeaseClient(Guid.NewGuid().ToString());
        await abandoned.AcquireAsync(TimeSpan.FromSeconds(-1));
        var coordinator=new FinanceSourceCoordinator(configuration);
        try
        {
            using var wait=new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>coordinator.AcquireAsync(tenant,wait.Token));
            Assert.Equal(LeaseState.Leased,(await blob.GetPropertiesAsync()).Value.LeaseState);
            // No writer remains in this fixture. Recovery is a distinct, explicit admin operation.
            await abandoned.BreakAsync(TimeSpan.Zero);
            await using(await coordinator.AcquireAsync(tenant)) { }
            Assert.Equal(LeaseState.Available,(await blob.GetPropertiesAsync()).Value.LeaseState);
        }
        finally
        {
            if((await blob.GetPropertiesAsync()).Value.LeaseState==LeaseState.Leased)await abandoned.BreakAsync(TimeSpan.Zero);
            await blob.DeleteIfExistsAsync();
        }
    }

    [AzuriteFact][Trait("Category", "StorageIntegration")]
    public async Task InvoiceMarkerAndPayloadPointerUseFreshPhysicalVersionsWithoutEmployeeContext()
    {
        var prefix = "InvoiceTest" + Guid.NewGuid().ToString("N")[..12];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IBeam:Repositories:AzureTables:ConnectionString"] = "UseDevelopmentStorage=true",
            ["IBeam:Repositories:AzureTables:TableNamePrefix"] = prefix
        }).Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        TurnKeyOps.Repositories.Interfaces.IInvoiceRepository repository = new InvoiceRepository(Mock.Of<IAzureTablesRepositoryStore<Invoice>>(), cache,
            Mock.Of<ITenantContext>(), Options.Create(new RepositoryOptions()), configuration);
        var id = Guid.NewGuid(); var tenant = Guid.NewGuid();
        var source = new Invoice { Id = id, PartitionKey = tenant.ToString("N"), RowKey = id.ToString("N"),
            FinanceDispatchPending = true, WorkflowPayloadBlobName = $"{tenant:N}/{id:N}/event.json" };
        try
        {
            await repository.SaveAsync(source);
            var stored = (await repository.GetAsync(source.PartitionKey, source.RowKey))!;
            Assert.NotEmpty(stored.ETag.ToString());
            Assert.True(stored.FinanceDispatchPending);
            Assert.Equal(source.WorkflowPayloadBlobName, stored.WorkflowPayloadBlobName);
            Assert.Single(await repository.ListFinanceDispatchPendingAsync(250));
            var stale = JobConfigurationService.Clone(stored);
            stored.FinanceDispatchPending = false;
            await repository.SaveAsync(stored);
            Assert.Empty(await repository.ListFinanceDispatchPendingAsync(250));
            Assert.NotEqual(stale.ETag, stored.ETag);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(stale));
            Assert.False((await repository.GetAsync(source.PartitionKey, source.RowKey))!.FinanceDispatchPending);
            Assert.Null(await repository.GetAsync(Guid.NewGuid().ToString("N"), source.RowKey));
            var olderRow = new Invoice { Id=Guid.Parse("00000000-0000-0000-0000-000000000001"),PartitionKey=source.PartitionKey,
                RowKey="00000000000000000000000000000001",FinanceDispatchPending=true,FinanceDispatchAttemptedAtUtc=DateTime.UtcNow.AddMinutes(-1) };
            var freshRow = new Invoice { Id=Guid.Parse("00000000-0000-0000-0000-000000000002"),PartitionKey=source.PartitionKey,
                RowKey="00000000000000000000000000000002",FinanceDispatchPending=true };
            await repository.SaveAsync(olderRow);await repository.SaveAsync(freshRow);
            Assert.Equal(freshRow.Id,Assert.Single(await repository.ListFinanceDispatchPendingAsync(1)).Id);
        }
        finally { await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient(prefix + "Invoices").DeleteAsync(); }
    }

    [AzuriteFact][Trait("Category", "StorageIntegration")]
    public async Task FinanceSnapshotsRoundTripThroughRealBlobAndConditionalTablePointer()
    {
        var tenant = Guid.NewGuid(); var prefix = "FinanceTest" + Guid.NewGuid().ToString("N")[..12];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IBeam:Repositories:AzureTables:ConnectionString"] = "UseDevelopmentStorage=true",
            ["IBeam:Repositories:AzureTables:TableNamePrefix"] = prefix
        }).Build();
        var client = new BlobServiceClient("UseDevelopmentStorage=true", new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_12_02));
        var container = client.GetBlobContainerClient(FinanceStore.Container);
        await container.CreateIfNotExistsAsync();
        var transport = new Mock<IAzureBlobStorageService>();
        transport.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (string name, string blob, Stream stream, string type, IReadOnlyDictionary<string, string>? metadata, CancellationToken ct) =>
                await client.GetBlobContainerClient(name).GetBlobClient(blob).UploadAsync(stream,
                    new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = type }, Metadata = metadata is null ? null : new Dictionary<string, string>(metadata) }, ct));
        transport.Setup(b => b.OpenReadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, string blob, CancellationToken ct) => client.GetBlobContainerClient(name).GetBlobClient(blob).OpenReadAsync(cancellationToken: ct));
        transport.Setup(b => b.DeleteIfExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string name, string blob, CancellationToken ct) => { await client.GetBlobContainerClient(name).GetBlobClient(blob).DeleteIfExistsAsync(cancellationToken: ct); });
        var store = new FinanceStore(configuration, transport.Object);
        try
        {
            await store.SaveAsync(tenant, FinanceTests.State(), "");
            var original = await store.ReadAsync(tenant);
            Assert.NotEmpty(original.Version);
            var posting = JobConfigurationService.Clone(original);
            FinanceLedger.Post(posting, new(2026, 9, 15), "manual", "real-blob-opening", "Opening cash", [new("1000", 125, 0), new("3000", 0, 125)], "owner");
            await store.SaveAsync(tenant, posting, original.Version);
            var final = await store.ReadAsync(tenant);
            Assert.Equal(125, FinanceLedger.AccountBalance(final, "1000", DateOnly.MaxValue));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(tenant, original, original.Version));
            Assert.Empty((await store.ReadAsync(Guid.NewGuid())).Accounts);
            var retained = new List<string>();
            await foreach (var item in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: $"{tenant:N}/", cancellationToken: CancellationToken.None)) retained.Add(item.Name);
            Assert.Equal(2, retained.Count); // Both committed immutable snapshots survive.
            foreach (var blob in retained)
            {
                var properties = (await container.GetBlobClient(blob).GetPropertiesAsync()).Value;
                Assert.Equal(tenant.ToString("N"), properties.Metadata["tenantId"]);
                Assert.Equal("application/json", properties.ContentType);
            }
            final.Journals.Clear();
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(tenant, final, final.Version));
        }
        finally
        {
            await foreach (var item in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: $"{tenant:N}/", cancellationToken: CancellationToken.None)) await container.GetBlobClient(item.Name).DeleteIfExistsAsync();
            await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient(prefix + "FinanceVersions").DeleteAsync();
        }
    }

    [AzuriteFact][Trait("Category", "StorageIntegration")]
    public async Task InfiniteSourceFenceSerializesWritersWithoutBlockingOtherTenant()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["IBeam:Repositories:AzureTables:ConnectionString"] = "UseDevelopmentStorage=true" }).Build();
        var coordinator = new FinanceSourceCoordinator(configuration);
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid();
        var container = new BlobServiceClient("UseDevelopmentStorage=true", new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_12_02)).GetBlobContainerClient(FinanceSourceCoordinator.Container);
        var fence = await coordinator.AcquireAsync(tenant);
        try
        {
            var properties = await container.GetBlobClient($"{tenant:N}/invoice-source").GetPropertiesAsync();
            Assert.Equal(LeaseDurationType.Infinite, properties.Value.LeaseDuration);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.AcquireAsync(tenant, timeout.Token));
            await using var isolated = await coordinator.AcquireAsync(other);
        }
        finally { await fence.DisposeAsync(); }
        await using (await coordinator.AcquireAsync(tenant)) { }
        await container.GetBlobClient($"{tenant:N}/invoice-source").DeleteIfExistsAsync();
        await container.GetBlobClient($"{other:N}/invoice-source").DeleteIfExistsAsync();
    }
}
