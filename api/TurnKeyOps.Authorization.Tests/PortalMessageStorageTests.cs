using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.API.Configurations;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
namespace TurnKeyOps.Authorization.Tests;
public sealed class PortalMessageStorageTests
{
    [AzuriteFact][Trait("Category","StorageIntegration")]
    public async Task CustomerMessageRoundTripsWithoutEmployeeTenantContext()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IBeam:Repositories:AzureTables:ConnectionString"]="UseDevelopmentStorage=true"}).Build();
        var services=new ServiceCollection();services.AddLogging();services.AddMemoryCache();services.AddSingleton<IConfiguration>(config);services.AddSingleton<ITenantContext>(new TenantContext());services.ConfigureIBeamAzureTables(config);services.AddIBeamAzureTablesRepositories();services.AddAzureTableMappings();
        await using var provider=services.BuildServiceProvider();var repo=new ChatMessageRepository(provider.GetRequiredService<IAzureTablesRepositoryStore<ChatMessage>>(),provider.GetRequiredService<IMemoryCache>(),new TenantContext(),Options.Create(new RepositoryOptions()));
        var tenant=Guid.NewGuid();var chat=Guid.NewGuid();var id=Guid.NewGuid();var partition=TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(tenant)+"|PORTAL";var row=RepositoryKeyHelper.GetOrderedRowKeyPrefix(chat)+id.ToString("N");
        try{
            await repo.AppendCustomerMessageAsync(new(){Id=id,MessageId=id,ChatId=chat,TenantId=tenant,ActorUserId=Guid.NewGuid(),PartitionKey=partition,RowKey=row,Role="portal-customer",Content="Scoped storage test",ChatTimestamp=DateTime.UtcNow});
            var message=Assert.Single(await repo.GetMessagesByChatAsync(partition,chat,200));Assert.Equal(id,message.Id);Assert.Equal(tenant,message.TenantId);Assert.Equal("Scoped storage test",message.Content);
            Assert.Empty(await repo.GetMessagesByChatAsync(partition+"other",chat,200));
            var audits=new AuditEventRepository(provider.GetRequiredService<IAzureTablesRepositoryStore<AuditEvent>>(),provider.GetRequiredService<IMemoryCache>(),new TenantContext(),Options.Create(new RepositoryOptions()));
            var auditService=new MedInsights.Services.AuditService(audits,Moq.Mock.Of<MedInsights.Lib.Utils.IUserContext>());
            await auditService.RecordAsync(new(){TenantId=tenant,UserId=message.ActorUserId,Category="customer-portal",Action="message.submitted"});
            var record=Assert.Single(await audits.GetByTenantAsync(tenant));Assert.Equal(message.ActorUserId,record.UserId);
            await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("AuditEvents").DeleteEntityAsync(record.PartitionKey,record.RowKey,ETag.All);
        }finally{await new TableServiceClient("UseDevelopmentStorage=true").GetTableClient("ChatMessages").DeleteEntityAsync(partition,row,ETag.All);}
    }
}
