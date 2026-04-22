using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IUserProfileRepository : IBaseRepositoryAsync<UserProfile>
    {
        Task<UserProfile?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<Guid>> GetTenantIdsAsync(CancellationToken ct = default);
        Task<(IEnumerable<UserProfile> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);
    }
}
