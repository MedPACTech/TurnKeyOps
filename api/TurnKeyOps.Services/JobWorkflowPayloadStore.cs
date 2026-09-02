using System.Text.Json;
using MedInsights.AzureServices.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class JobWorkflowPayloadStore : IJobWorkflowPayloadStore
{
    internal const string ContainerName = "job-workflows";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAzureBlobStorageService _blobStorage;

    public JobWorkflowPayloadStore(IAzureBlobStorageService blobStorage) => _blobStorage = blobStorage;

    public async Task<string> SaveAsync(Guid tenantId, Guid jobId, JobWorkflowPayloadDto payload, CancellationToken ct = default)
    {
        var blobName = $"{tenantId:N}/{jobId:N}/{Guid.NewGuid():N}.json";
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        await _blobStorage.UploadAsync(ContainerName, blobName, content, "application/json", new Dictionary<string, string>
        {
            ["tenantId"] = tenantId.ToString("N"),
            ["jobId"] = jobId.ToString("N")
        }, ct);
        return blobName;
    }

    public async Task<JobWorkflowPayloadDto> LoadAsync(string? blobName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(blobName)) return new JobWorkflowPayloadDto();
        await using var content = await _blobStorage.OpenReadAsync(ContainerName, blobName, ct);
        return await JsonSerializer.DeserializeAsync<JobWorkflowPayloadDto>(content, JsonOptions, ct)
            ?? throw new InvalidOperationException("Job workflow payload is invalid.");
    }

    public Task DeleteIfExistsAsync(string? blobName, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(blobName)
            ? Task.CompletedTask
            : _blobStorage.DeleteIfExistsAsync(ContainerName, blobName, ct);
}
