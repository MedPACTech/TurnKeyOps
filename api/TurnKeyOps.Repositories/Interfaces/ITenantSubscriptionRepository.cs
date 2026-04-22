using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantSubscriptionRepository : IBaseRepositoryAsync<TenantSubscription>
    {
        Task<TenantSubscription?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<TenantSubscription?> GetCurrentAsync(string partitionKey, CancellationToken ct = default);
        Task<(IEnumerable<TenantSubscription> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);
        Task<TenantSubscription?> GetByProviderSubscriptionIdAsync(string provider, string providerSubscriptionId, CancellationToken ct = default);
        Task<IReadOnlyList<TenantSubscription>> GetAllActiveAsync(CancellationToken ct = default);
        Task<IReadOnlyList<TenantSubscription>> GetRenewalDueAsync(DateTime dueBeforeUtc, CancellationToken ct = default);
    }
}
