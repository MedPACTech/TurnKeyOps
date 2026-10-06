using System.Text.Json;
using MedInsights.AzureServices.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Services;

// Same immutable blob snapshot pattern as existing JobWorkflowPayloadStore; keep table envelopes bounded.
public sealed class LeadActivityStore(IAzureBlobStorageService blobs, ILeadRepository repository)
{
    private const string Container = "lead-activity";
    public async Task HydrateAsync(Lead entity, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entity.ActivityBlobName)) return;
        if (!entity.ActivityBlobName.StartsWith($"{entity.Data.TenantId:N}/{entity.Id:N}/", StringComparison.Ordinal))
            throw new InvalidOperationException("Lead activity scope is invalid.");
        await using var stream = await blobs.OpenReadAsync(Container, entity.ActivityBlobName, ct);
        entity.Data.Activity = await JsonSerializer.DeserializeAsync<List<LeadActivityDto>>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException("Lead activity could not be read.");
    }
    public async Task<Lead> CommitAsync(Lead entity, bool create, CancellationToken ct)
    {
        var history = entity.Data.Activity;
        var name = $"{entity.Data.TenantId:N}/{entity.Id:N}/{entity.Data.Version}.json";
        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(history));
        await blobs.UploadAsync(Container, name, content, "application/json", new Dictionary<string,string> { ["tenantId"] = entity.Data.TenantId.ToString("N") }, ct);
        entity.ActivityBlobName = name;
        entity.Data.Activity = [];
        try { var saved = await repository.CommitAsync(entity, create, ct); saved.Data.Activity = history; return saved; }
        finally { entity.Data.Activity = history; }
    }
}
