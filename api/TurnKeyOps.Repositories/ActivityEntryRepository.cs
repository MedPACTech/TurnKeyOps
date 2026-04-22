using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class ActivityEntryRepository : AzureTablesRepositoryBase<ActivityItems>, IActivityEntryRepository
    {
        private readonly IAzureTablesRepositoryStore<ActivityItems> _azureStore;

        public ActivityEntryRepository(
            IAzureTablesRepositoryStore<ActivityItems> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<ActivityItems?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            return entity;
        }

        public async Task<IEnumerable<ActivityItems>> GetEntryForUserByDateAsync(string partitionKey, DateTime entryDate, Guid userId, CancellationToken ct = default)
        {
            var rkPrefix = $"DATE|{entryDate:yyyyMMdd}|USER|{userId}|ITEM|";

            var results = new List<ActivityItems>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == partitionKey && !e.IsDeleted, ct, "IsDeleted"))
            {
                if (!string.IsNullOrWhiteSpace(entity.RowKey) && entity.RowKey.StartsWith(rkPrefix, StringComparison.Ordinal))
                    results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<ActivityItems>> GetForMonthAsync(string partitionKey, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                throw new ArgumentException("Partition key is required.", nameof(partitionKey));

            var results = new List<ActivityItems>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == partitionKey && !e.IsDeleted, ct, "IsDeleted"))
                results.Add(entity);

            return results;
        }

        public async Task UpsertBatchAsync(IEnumerable<ActivityItems> entities, CancellationToken ct = default)
        {
            var entityList = entities.ToList();
            if (entityList.Count == 0) return;

            foreach (var entity in entityList)
            {
                entity.IsDeleted = false;
                await SaveAsync(entity, ct);
            }
        }
    }
}
