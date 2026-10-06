using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Services;

// Internal integration boundary: only trusted, persisted intake may call this. No public route accepts a tenant ID.
public sealed class LeadIntakeBridge(ILeadRepository leads, LeadConfigurationService configuration)
{
    public async Task<Lead> EnsureAsync(QuoteRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty || request.IsDeleted || request.PartitionKey != RepositoryKeyHelper.ToTenantPartitionKey(request.TenantId))
            throw new ArgumentException("A persisted tenant intake record is required.");
        var existing = await leads.GetAsync(request.PartitionKey, RepositoryKeyHelper.ToRowKey(request.Id), ct);
        if (existing is not null) return existing; // Intake retries never reset authoritative opportunity workflow.
        var config = await configuration.GetAsync(request.TenantId, ct);
        var data = new LeadDto
        {
            Id = request.Id, TenantId = request.TenantId, IntakeRequestId = request.Id,
            Title = string.IsNullOrWhiteSpace(request.SiteName) ? $"{request.ContactName} · {request.ServiceType}" : request.SiteName,
            ContactName = request.ContactName, CompanyName = request.CompanyName, Email = request.Email, Phone = request.Phone,
            SiteAddress = request.ServiceAddress, RequestedWork = request.Need, Service = request.ServiceType,
            PropertyType = request.PropertyType, TradeProfile = config.DefaultTradeProfile,
            Source = request.Source == "public-site" ? "Website" : "Manual", Stage = LeadStages.FromIntake(request.Status),
            CreatedAtUtc = request.SubmittedAtUtc, UpdatedAtUtc = request.DateUpdated,
            NextAction = LeadStages.NextAction(LeadStages.FromIntake(request.Status)),
            Attribution = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.AttributionJson) ?? [],
            Version = Guid.NewGuid().ToString("N"),
            Activity = [new() { Type = "intake", Actor = "Customer", Text = "Intake received; original submission, files and history retained.", RelatedId = request.Id, OccurredAtUtc = request.SubmittedAtUtc }]
        };
        data.Attribution["intakeSource"] = request.Source;
        data.Attribution["formType"] = request.PropertyType;
        return await leads.CommitAsync(new Lead
        {
            Id = data.Id, PartitionKey = request.PartitionKey, RowKey = RepositoryKeyHelper.ToRowKey(data.Id),
            Data = data, DateCreated = data.CreatedAtUtc, DateUpdated = data.UpdatedAtUtc
        }, true, ct);
    }
}
