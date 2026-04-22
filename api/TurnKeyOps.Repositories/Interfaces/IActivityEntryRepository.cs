using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IActivityEntryRepository : IBaseRepositoryAsync<ActivityItems>
    {
        Task UpsertBatchAsync(IEnumerable<ActivityItems> entities, CancellationToken ct = default);
        Task<ActivityItems?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IEnumerable<ActivityItems>> GetEntryForUserByDateAsync(string partitionKey, DateTime entryDate, Guid userId, CancellationToken ct = default);
        Task<IReadOnlyList<ActivityItems>> GetForMonthAsync(string partitionKey, CancellationToken ct = default);
    }
}
