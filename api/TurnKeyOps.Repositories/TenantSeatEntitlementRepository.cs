using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class TenantSeatEntitlementRepository : AzureTablesRepositoryBase<TenantSeatEntitlement>, ITenantSeatEntitlementRepository
    {
        private readonly IAzureTablesRepositoryStore<TenantSeatEntitlement> _azureStore;

        public TenantSeatEntitlementRepository(
            IAzureTablesRepositoryStore<TenantSeatEntitlement> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<TenantSeatEntitlement?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<TenantSeatEntitlement?> GetCurrentAsync(string partitionKey, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.PartitionKey == partitionKey && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                return entity;
            }

            return null;
        }

        public async Task<TenantSeatEntitlement?> GetBySubscriptionIdAsync(string partitionKey, Guid subscriptionId, CancellationToken ct = default)
        {
            var rowKey = $"SEATS|{subscriptionId:N}";
            return await GetAsync(partitionKey, rowKey, ct);
        }
    }
}
