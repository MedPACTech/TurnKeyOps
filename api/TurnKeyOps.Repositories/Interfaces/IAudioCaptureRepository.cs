using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IAudioCaptureRepository : IBaseRepositoryAsync<AudioCapture>
    {
        Task<(IEnumerable<AudioCapture> Results, string? ContinuationToken)> GetAudioCapturesByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);

        Task<IReadOnlyList<AudioCapture>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default);
        Task<AudioCapture?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<AudioCapture?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default);
    }
}
