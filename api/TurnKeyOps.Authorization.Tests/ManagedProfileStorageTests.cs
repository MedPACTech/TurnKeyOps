using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.API.Configurations;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TurnKeyOps.Authorization.Tests;

public sealed class AzuriteFactAttribute : FactAttribute
{
    public AzuriteFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TKO_STORAGE_TESTS") != "1")
            Skip = "Run with TKO_STORAGE_TESTS=1 and local Azurite (also exercised in CI).";
    }
}
public sealed class ManagedProfileStorageTests
{
    [AzuriteFact]
    [Trait("Category", "StorageIntegration")]
    public async Task Envelope_preserves_versions_archived_records_and_explicit_empty_permissions()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["IBeam:Repositories:AzureTables:ConnectionString"] = "UseDevelopmentStorage=true"
        }).Build();
        RepositoryKeyHelper.ConfigureGuidKeyFormat("D");
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<ITenantContext>(new TenantContext());
        services.ConfigureIBeamAzureTables(config);
        services.AddIBeamAzureTablesRepositories(); services.AddAzureTableMappings();
        await using var provider = services.BuildServiceProvider();
        var store = new ManagedProfileStore(provider.GetRequiredService<IAzureTablesRepositoryStore<UserProfile>>(), config);
        var tenant = Guid.NewGuid(); var id = Guid.NewGuid();
        var pk = EntityKeyPolicy.TenantPartition(tenant); var rk = EntityKeyPolicy.Row(id);
        var table = new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("UserProfiles");
        try {
            await store.AddAsync(tenant, new UserProfile { Id=id, ApplicationUserId=id, PartitionKey=pk, RowKey=rk,
                FirstName="Storage fixture", Role="admin", IsActive=true, ProfileTypes=["employee","vendor"],
                ModulePermissions=["jobs.read"] }, default);
            var saved = (await store.GetByKeysAsync(pk,rk,default))!;
            Assert.Equal(new[]{"employee","vendor"}, saved.ProfileTypes);
            Assert.Equal(new[]{"jobs.read"}, saved.ModulePermissions);
            var oldVersion = saved.ETag;
            Assert.False(string.IsNullOrWhiteSpace(oldVersion.ToString()));
            saved.ModulePermissions=[]; saved.IsActive=false; saved.IsDeleted=true;
            await store.UpdateAsync(tenant,saved,TableUpdateMode.Replace,oldVersion,default);
            var stale = await Assert.ThrowsAnyAsync<Exception>(() => store.UpdateAsync(tenant,saved,TableUpdateMode.Replace,oldVersion,default));
            Assert.True(stale is RequestFailedException { Status:412 } || stale.InnerException is RequestFailedException { Status:412 });
            var rows = new List<UserProfile>();
            await foreach(var row in store.ListAsync(pk,default)) rows.Add(row);
            var archived = Assert.Single(rows);
            Assert.True(archived.IsDeleted); Assert.Empty(archived.ModulePermissions!);
            Assert.NotEqual(oldVersion,archived.ETag);
            archived.IsDeleted=false; archived.IsActive=true;
            await store.UpdateAsync(tenant,archived,TableUpdateMode.Replace,archived.ETag,default);
            Assert.False((await store.GetByKeysAsync(pk,rk,default))!.IsDeleted);
            await Assert.ThrowsAsync<ArgumentException>(() => store.UpdateAsync(tenant,archived,TableUpdateMode.Replace,ETag.All,default));
            Assert.Null(await store.GetByKeysAsync(EntityKeyPolicy.TenantPartition(Guid.NewGuid()),rk,default));
        } finally { await table.DeleteEntityAsync(pk,rk,ETag.All); }
    }
}
