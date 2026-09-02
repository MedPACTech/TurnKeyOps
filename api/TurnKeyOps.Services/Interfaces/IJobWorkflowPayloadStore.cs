using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IJobWorkflowPayloadStore
{
    Task<string> SaveAsync(Guid tenantId, Guid jobId, JobWorkflowPayloadDto payload, CancellationToken ct = default);
    Task<JobWorkflowPayloadDto> LoadAsync(string? blobName, CancellationToken ct = default);
    Task DeleteIfExistsAsync(string? blobName, CancellationToken ct = default);
}
