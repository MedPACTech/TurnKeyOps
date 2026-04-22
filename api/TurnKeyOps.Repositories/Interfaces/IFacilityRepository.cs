using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IFacilityRepository : IBaseRepositoryAsync<Facility>
    {
        Task<Facility?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<Facility>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default);
    }
}
