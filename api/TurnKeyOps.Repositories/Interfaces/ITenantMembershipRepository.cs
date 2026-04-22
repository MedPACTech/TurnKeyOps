using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantMembershipRepository : IBaseRepositoryAsync<TenantMembership>
    {
        Task<TenantMembership?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<TenantMembership?> GetByUserIdAsync(string partitionKey, Guid userId, CancellationToken ct = default);
        Task<IReadOnlyList<TenantMembership>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
        Task<(IEnumerable<TenantMembership> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);
        Task<IReadOnlyList<TenantMembership>> GetActiveAssignedByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task<IReadOnlyList<Guid>> GetTenantIdsAsync(CancellationToken ct = default);
    }
}
