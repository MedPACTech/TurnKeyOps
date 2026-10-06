using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Services;

public sealed class LeadConfigurationService(ITenantSettingsRepository settings)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<LeadConfigurationDto> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        var document = await settings.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(tenantId), "SETTINGS|OPERATIONAL", ct);
        if (document is null || document.IsDeleted) return new();
        using var values = JsonDocument.Parse(document.ValuesJson);
        return values.RootElement.TryGetProperty("leads", out var leads)
            ? leads.Deserialize<LeadConfigurationDto>(Json) ?? new()
            : LocksmithPolicy.IsConfigured(document.ValuesJson) ? new() { DefaultTradeProfile = "doors-locks" } : new();
    }

    public async Task<TenantSettingsDocumentDto> UpdateAsync(Guid tenantId, LeadConfigurationDto config, string version,
        TurnKeyOps.Services.Interfaces.ITenantSettingsService service, CancellationToken ct)
    {
        var document = await settings.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(tenantId), "SETTINGS|OPERATIONAL", ct);
        var values = System.Text.Json.Nodes.JsonNode.Parse(document?.ValuesJson ?? "{}")!.AsObject();
        values["leads"] = JsonSerializer.SerializeToNode(config, Json);
        // Keep unrelated operational settings and server-only secret references intact.
        return await service.UpsertAsync("operational", new UpdateTenantSettingsDocumentDto {
            ExpectedVersion = version, SchemaVersion = document?.SchemaVersion ?? 1,
            Values = JsonSerializer.SerializeToElement(values),
            SecretReferences = JsonSerializer.Deserialize<Dictionary<string, string>>(document?.SecretReferencesJson ?? "{}") ?? []
        }, ct);
    }

    public static void Validate(JsonElement value)
    {
        var config = value.Deserialize<LeadConfigurationDto>(Json) ?? throw new ArgumentException("Leads settings are required.");
        if (config.TradeProfiles.Length == 0 || !config.TradeProfiles.Contains(config.DefaultTradeProfile))
            throw new ArgumentException("Select at least one trade and a default from those trades.");
        if (config.StageLabels.Any(x => !LeadStages.All.Contains(x.Key) || string.IsNullOrWhiteSpace(x.Value) || x.Value.Length > 80))
            throw new ArgumentException("Stage labels must map to canonical Lead stages.");
        if (config.AssignmentMode is not ("manual" or "rules" or "round-robin" or "workload"))
            throw new ArgumentException("Unsupported assignment mode.");
        if (config.AssignmentRules.Count > 100 || config.AssignmentRules.Any(x => x.MembershipId == Guid.Empty))
            throw new ArgumentException("Assignment rules need a member and are limited to 100.");
        if (config.RequiredFields.Any(x => x.Value.Count > 30 || x.Value.Any(f => string.IsNullOrWhiteSpace(f.Key) || f.Key.Length > 80 || f.Value.Length > 120)))
            throw new ArgumentException("Qualification fields must have short keys and labels; maximum 30 per trade.");
        if (config.AiActions.Any(x => x.Value is not ("disabled" or "read" or "recommend" or "draft" or "approval" or "auto")))
            throw new ArgumentException("Unknown Bob action authority.");
        if (config.WonReasons.Length == 0 || config.LostReasons.Length == 0 || config.WonReasons.Concat(config.LostReasons).Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 120))
            throw new ArgumentException("Provide short won and lost reasons.");
    }

    public static IReadOnlyList<LeadFieldDto> Fields(LeadDto lead, LeadConfigurationDto config)
    {
        var fields = new Dictionary<string, string>();
        switch (lead.TradeProfile)
        {
            case "concrete": fields["dimensions"] = "Approximate dimensions"; fields["surface"] = "Existing surface"; break;
            case "framing": fields["dimensions"] = "Approximate dimensions"; fields["structure"] = "Structure type"; break;
            case "land-clearing": fields["acreage"] = "Approximate acreage"; fields["access"] = "Site access"; break;
            case "doors-locks": fields["openingCount"] = "Opening count"; fields["hardware"] = "Door / frame / hardware"; break;
        }
        if (config.RequiredFields.TryGetValue(lead.TradeProfile, out var overrides)) fields = overrides;
        var result = new List<LeadFieldDto>
        {
            new("requestedWork", "Requested work", true, lead.RequestedWork),
            new("contact", "Email or phone", true, string.IsNullOrWhiteSpace(lead.Email) ? lead.Phone : lead.Email),
            new("siteAddress", "Job site", true, lead.SiteAddress)
        };
        result.AddRange(fields.Select(x => new LeadFieldDto(x.Key, x.Value, true, lead.Qualification.GetValueOrDefault(x.Key, ""))));
        result.AddRange(lead.Qualification.Where(x => !fields.ContainsKey(x.Key)).Select(x => new LeadFieldDto(x.Key, x.Key, false, x.Value)));
        return result;
    }
}
