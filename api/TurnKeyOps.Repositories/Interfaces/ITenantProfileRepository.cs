using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantProfileRepository : IBaseRepositoryAsync<TenantProfile>
    {
        Task<TenantProfile?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<Guid>> GetTenantIdsAsync(CancellationToken ct = default);
    }
}
