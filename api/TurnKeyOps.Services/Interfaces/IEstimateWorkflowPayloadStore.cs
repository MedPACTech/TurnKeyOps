using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IEstimateWorkflowPayloadStore
{
    Task<string?> SaveEstimateStructuredInputAsync(Guid tenantId, Guid estimateId, StructuredEstimateInputDto? input, CancellationToken ct = default);
    Task<string?> SaveEstimateCalculationSnapshotAsync(Guid tenantId, Guid estimateId, EstimateCalculationSnapshotDto? snapshot, CancellationToken ct = default);
    Task<string?> SaveEstimateTranscriptAsync(Guid tenantId, Guid estimateId, IReadOnlyCollection<BobTranscriptEntryDto>? transcript, CancellationToken ct = default);
    Task<string?> SaveJobEstimateSnapshotAsync(Guid tenantId, Guid jobId, EstimateCalculationSnapshotDto? snapshot, CancellationToken ct = default);
    Task<StructuredEstimateInputDto?> LoadEstimateStructuredInputAsync(string? blobName, string? legacyJson = null, CancellationToken ct = default);
    Task<EstimateCalculationSnapshotDto?> LoadEstimateCalculationSnapshotAsync(string? blobName, string? legacyJson = null, CancellationToken ct = default);
    Task<IReadOnlyList<BobTranscriptEntryDto>> LoadEstimateTranscriptAsync(string? blobName, CancellationToken ct = default);
    Task<EstimateCalculationSnapshotDto?> LoadJobEstimateSnapshotAsync(string? blobName, string? legacyJson = null, CancellationToken ct = default);
}
