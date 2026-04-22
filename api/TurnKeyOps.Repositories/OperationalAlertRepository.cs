using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class OperationalAlertRepository : AzureTablesRepositoryBase<OperationalAlert>, IOperationalAlertRepository
    {
        private readonly IAzureTablesRepositoryStore<OperationalAlert> _azureStore;

        public OperationalAlertRepository(
            IAzureTablesRepositoryStore<OperationalAlert> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<OperationalAlert?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<OperationalAlert?> GetByDedupeKeyAsync(string partitionKey, string dedupeKey, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.PartitionKey == partitionKey && e.DedupeKey == dedupeKey && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                return entity;
            }

            return null;
        }

        public async Task<IReadOnlyList<OperationalAlert>> GetByTenantAsync(Guid? tenantId, string? status = null, int take = 100, CancellationToken ct = default)
        {
            var results = new List<OperationalAlert>();
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.TenantId == tenantId && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                if (string.IsNullOrWhiteSpace(status) || string.Equals(entity.Status, status, StringComparison.OrdinalIgnoreCase))
                    results.Add(entity);
            }

            return results.OrderByDescending(x => x.LastOccurredUtc).Take(take).ToList();
        }
    }
}
