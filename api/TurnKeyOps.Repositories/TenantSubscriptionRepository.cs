using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class TenantSubscriptionRepository : AzureTablesRepositoryBase<TenantSubscription>, ITenantSubscriptionRepository
    {
        private readonly IAzureTablesRepositoryStore<TenantSubscription> _azureStore;

        public TenantSubscriptionRepository(
            IAzureTablesRepositoryStore<TenantSubscription> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<TenantSubscription?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<TenantSubscription?> GetCurrentAsync(string partitionKey, CancellationToken ct = default)
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

        public async Task<(IEnumerable<TenantSubscription> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default)
            => await _azureStore.GetByPartitionPagedAsync(partitionKey, pageSize, continuationToken, ct, "IsDeleted");

        public async Task<TenantSubscription?> GetByProviderSubscriptionIdAsync(string provider, string providerSubscriptionId, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.Provider == provider && e.ProviderSubscriptionId == providerSubscriptionId && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                return entity;
            }

            return null;
        }

        public async Task<IReadOnlyList<TenantSubscription>> GetAllActiveAsync(CancellationToken ct = default)
        {
            var results = new List<TenantSubscription>();
            await foreach (var entity in _azureStore.QueryAsync(
                               e => !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                if (IsActiveStatus(entity.SubscriptionStatus))
                    results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<TenantSubscription>> GetRenewalDueAsync(DateTime dueBeforeUtc, CancellationToken ct = default)
        {
            var results = new List<TenantSubscription>();
            await foreach (var entity in _azureStore.QueryAsync(
                               e => !e.IsDeleted && e.TermEndUtc <= dueBeforeUtc,
                               ct,
                               "IsDeleted"))
            {
                if (IsActiveStatus(entity.SubscriptionStatus))
                    results.Add(entity);
            }

            return results;
        }

        private static bool IsActiveStatus(string? status)
            => string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "trialing", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "past_due", StringComparison.OrdinalIgnoreCase);
    }
}
