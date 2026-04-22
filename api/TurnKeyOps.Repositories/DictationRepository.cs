using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class DictationRepository : AzureTablesRepositoryBase<Dictation>, IDictationRepository
    {
        private readonly IAzureTablesRepositoryStore<Dictation> _azureStore;

        public DictationRepository(
            IAzureTablesRepositoryStore<Dictation> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<(IEnumerable<Dictation> Results, string? ContinuationToken)> GetDictationsByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default)
        {
            var (results, nextToken) = await _azureStore.GetByPartitionPagedAsync(partitionKey, pageSize, continuationToken, ct, "IsDeleted");
            foreach (var item in results)
                SyncEntityIdentityFromKeys(item);

            return (results, nextToken);
        }

        public async Task<IReadOnlyList<Dictation>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default)
        {
            var results = new List<Dictation>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == partitionKey && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<Dictation?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        private static void SyncEntityIdentityFromKeys(Dictation entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
