using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class TenantBillingAccountRepository : AzureTablesRepositoryBase<TenantBillingAccount>, ITenantBillingAccountRepository
    {
        private readonly IAzureTablesRepositoryStore<TenantBillingAccount> _azureStore;

        public TenantBillingAccountRepository(
            IAzureTablesRepositoryStore<TenantBillingAccount> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<TenantBillingAccount?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<TenantBillingAccount?> GetByProviderCustomerIdAsync(string provider, string providerCustomerId, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.Provider == provider && e.ProviderCustomerId == providerCustomerId && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                return entity;
            }

            return null;
        }
    }
}
