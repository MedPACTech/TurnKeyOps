using System.Text;
using System.Text.Json;
using MedInsights.AzureServices.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class EstimateWorkflowPayloadStore : IEstimateWorkflowPayloadStore
{
    private const string EstimateStructuredInputContainer = "estimate-structured-inputs";
    private const string EstimateCalculationSnapshotContainer = "estimate-calculation-snapshots";
    private const string EstimateTranscriptContainer = "estimate-bob-transcripts";
    private const string JobEstimateSnapshotContainer = "job-estimate-snapshots";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAzureBlobStorageService _blobStorageService;

    public EstimateWorkflowPayloadStore(IAzureBlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    public Task<string?> SaveEstimateStructuredInputAsync(Guid tenantId, Guid estimateId, StructuredEstimateInputDto? input, CancellationToken ct = default) =>
        SaveJsonAsync(EstimateStructuredInputContainer, BuildBlobName(tenantId, estimateId, "structured-input"), input, ct);

    public Task<string?> SaveEstimateCalculationSnapshotAsync(Guid tenantId, Guid estimateId, EstimateCalculationSnapshotDto? snapshot, CancellationToken ct = default) =>
        SaveJsonAsync(EstimateCalculationSnapshotContainer, BuildBlobName(tenantId, estimateId, "calculation-snapshot"), snapshot, ct);

    public Task<string?> SaveEstimateTranscriptAsync(Guid tenantId, Guid estimateId, IReadOnlyCollection<BobTranscriptEntryDto>? transcript, CancellationToken ct = default) =>
        SaveJsonAsync(
            EstimateTranscriptContainer,
            BuildBlobName(tenantId, estimateId, "bob-transcript"),
            transcript is null || transcript.Count == 0 ? null : transcript,
            ct);

    public Task<string?> SaveJobEstimateSnapshotAsync(Guid tenantId, Guid jobId, EstimateCalculationSnapshotDto? snapshot, CancellationToken ct = default) =>
        SaveJsonAsync(JobEstimateSnapshotContainer, BuildBlobName(tenantId, jobId, "estimate-snapshot"), snapshot, ct);

    public Task<StructuredEstimateInputDto?> LoadEstimateStructuredInputAsync(string? blobName, string? legacyJson = null, CancellationToken ct = default) =>
        LoadJsonAsync<StructuredEstimateInputDto>(EstimateStructuredInputContainer, blobName, legacyJson, ct);

    public Task<EstimateCalculationSnapshotDto?> LoadEstimateCalculationSnapshotAsync(string? blobName, string? legacyJson = null, CancellationToken ct = default) =>
        LoadJsonAsync<EstimateCalculationSnapshotDto>(EstimateCalculationSnapshotContainer, blobName, legacyJson, ct);

    public async Task<IReadOnlyList<BobTranscriptEntryDto>> LoadEstimateTranscriptAsync(string? blobName, CancellationToken ct = default)
        => await LoadJsonAsync<List<BobTranscriptEntryDto>>(EstimateTranscriptContainer, blobName, null, ct) ?? [];

    public Task<EstimateCalculationSnapshotDto?> LoadJobEstimateSnapshotAsync(string? blobName, string? legacyJson = null, CancellationToken ct = default) =>
        LoadJsonAsync<EstimateCalculationSnapshotDto>(JobEstimateSnapshotContainer, blobName, legacyJson, ct);

    private async Task<string?> SaveJsonAsync<T>(string container, string blobName, T? value, CancellationToken ct)
    {
        if (value is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        var saved = await _blobStorageService.Save(container, blobName, payload);
        if (!saved)
        {
            throw new InvalidOperationException($"Unable to persist workflow payload to blob storage container '{container}'.");
        }

        return blobName;
    }

    private async Task<T?> LoadJsonAsync<T>(string container, string? blobName, string? legacyJson, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(blobName))
        {
            var payload = await _blobStorageService.Get(container, blobName);
            if (payload is { Length: > 0 })
            {
                return JsonSerializer.Deserialize<T>(payload, JsonOptions);
            }
        }

        if (!string.IsNullOrWhiteSpace(legacyJson))
        {
            return JsonSerializer.Deserialize<T>(legacyJson, JsonOptions);
        }

        return default;
    }

    private static string BuildBlobName(Guid tenantId, Guid resourceId, string artifact)
        => $"{tenantId:D}/{resourceId:D}/{DateTime.UtcNow:yyyyMMddHHmmssfff}-{artifact}.json";
}
