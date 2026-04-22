using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ICaptureDraftNoteRepository : IBaseRepositoryAsync<CaptureDraftNote>
    {
        Task<(IEnumerable<CaptureDraftNote> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);

        Task<IReadOnlyList<CaptureDraftNote>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default);
        Task<IReadOnlyList<CaptureDraftNote>> GetByPartitionAndPatientAsync(string partitionKey, string patientRowKey, CancellationToken ct = default);
        Task<CaptureDraftNote?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
