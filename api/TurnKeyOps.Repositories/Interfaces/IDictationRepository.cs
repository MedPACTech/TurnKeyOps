using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IDictationRepository : IBaseRepositoryAsync<Dictation>
    {
        Task<(IEnumerable<Dictation> Results, string? ContinuationToken)> GetDictationsByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);

        Task<IReadOnlyList<Dictation>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default);
        Task<Dictation?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
