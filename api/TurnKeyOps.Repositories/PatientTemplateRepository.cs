using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class PromptTemplateRepository : AzureTablesRepositoryBase<PromptTemplate>, IPromptTemplateRepository
    {
        private readonly IAzureTablesRepositoryStore<PromptTemplate> _azureStore;

        public PromptTemplateRepository(
            IAzureTablesRepositoryStore<PromptTemplate> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<PromptTemplate?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity == null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IEnumerable<PromptTemplate>> GetAllAsync(string partitionKey)
        {
            var results = new List<PromptTemplate>();
            await foreach (var entity in _azureStore.QueryAsync(pt => pt.PartitionKey == partitionKey && !pt.IsDeleted, softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        private static void SyncEntityIdentityFromKeys(PromptTemplate entity)
        {
            if (entity.Id == Guid.Empty && Guid.TryParse(entity.RowKey, out var parsed))
                entity.Id = parsed;
        }
    }
}
