using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IInviteRepository : IBaseRepositoryAsync<Invite>
    {
        Task<Invite?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct = default, bool includeDeleted = false);
        Task<(IEnumerable<Invite> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);
    }
}
