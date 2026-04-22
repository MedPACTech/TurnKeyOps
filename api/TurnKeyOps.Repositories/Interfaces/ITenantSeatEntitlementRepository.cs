using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantSeatEntitlementRepository : IBaseRepositoryAsync<TenantSeatEntitlement>
    {
        Task<TenantSeatEntitlement?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<TenantSeatEntitlement?> GetCurrentAsync(string partitionKey, CancellationToken ct = default);
        Task<TenantSeatEntitlement?> GetBySubscriptionIdAsync(string partitionKey, Guid subscriptionId, CancellationToken ct = default);
    }
}
