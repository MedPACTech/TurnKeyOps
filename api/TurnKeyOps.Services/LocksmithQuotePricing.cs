using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Repositories.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Services;

public static class LocksmithQuotePricing
{
    public static string PolicyVersion(TenantSettingsDocument document) => !string.IsNullOrWhiteSpace(document.ETag.ToString()) ? document.ETag.ToString() : document.DateUpdated.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, Dictionary<string, decimal>> DefaultLaborHours = new() {
        ["residential"] = new() { ["Door and frame replacement"] = 4m, ["Lock and hardware installation"] = 1.5m, ["Repairs"] = 2m, ["Rekeying"] = 1m },
        ["commercial"] = new() { ["Door and frame replacement"] = 6m, ["Lock and hardware installation"] = 2m, ["Repairs"] = 3m, ["Rekeying"] = 1.5m }
    };
    public static async Task<(TenantSettingsDocument Settings, string[] Capabilities)> RequireContextAsync(
        ITenantSettingsRepository? settings, ITenantMembershipRepository? memberships, IUserContext user, string? jobType, CancellationToken ct, bool allowOfficeAdmin = false)
    {
        if (!user.IsAuthenticated || settings is null || memberships is null) throw new UnauthorizedAccessException("An authenticated field session is required.");
        var member = await memberships.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(user.TenantId), user.UserId, ct);
        if (member is null || member.TenantId != user.TenantId || member.UserId != user.UserId || member.IsDeleted || member.DateRemoved.HasValue ||
            !string.Equals(member.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase) || string.Equals(member.Role, "contact", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenAccessException("An active tenant technician membership is required.");
        var document = await settings.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId), "SETTINGS|OPERATIONAL", ct);
        if (document is null || document.IsDeleted || !LocksmithPolicy.IsConfigured(document.ValuesJson))
            throw new ArgumentException("Configure doors and locksmith pricing and technician capabilities before quoting.");
        var capabilities = allowOfficeAdmin && (member.IsOwner || TenantRoleCatalog.CanManageRoles(member.Role.Trim().ToLowerInvariant()))
            ? new[] { "residential", "commercial" } : LocksmithPolicy.CapabilitiesFor(document.ValuesJson, member.Id);
        if (capabilities.Length == 0 || (jobType is not null && !capabilities.Contains(jobType)))
            throw new ForbiddenAccessException("This technician is not enabled for the selected job type.");
        return (document, capabilities);
    }

    public static LocksmithQuoteContextDto Context(TenantSettingsDocument document, string[] capabilities)
    {
        using var json = JsonDocument.Parse(document.ValuesJson);
        var settings = json.RootElement.GetProperty("locksmith");
        LocksmithPolicy.Validate(settings);
        var pricing = settings.GetProperty("pricing");
        var defaults = DefaultLaborHours.ToDictionary(group => group.Key, group => new Dictionary<string, decimal>(group.Value));
        if (pricing.TryGetProperty("laborHoursByJobType", out var hours))
            foreach (var jobType in hours.EnumerateObject())
                foreach (var service in jobType.Value.EnumerateObject()) defaults[jobType.Name][service.Name] = service.Value.GetDecimal();
        var catalog = settings.TryGetProperty("catalog", out var items) && items.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<LocksmithCatalogItemDto>>(items.GetRawText(), JsonOptions) ?? [] : [];
        return new() {
            JobTypes = [.. capabilities], PolicyVersion = PolicyVersion(document),
            LaborRatePerHour = Number(pricing, "laborRatePerHour"), TaxPercent = Number(pricing, "taxPercent"),
            LaborHoursByJobType = defaults,
            Catalog = catalog.Where(item => !item.Sample && !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name) && item.UnitPrice >= 0 && item.JobTypes.Any(capabilities.Contains)).ToList()
        };
    }

    public static LocksmithPricingSnapshotDto Calculate(TenantSettingsDocument document, LocksmithQuoteInputDto input)
    {
        if (input.JobType is not ("residential" or "commercial")) throw new ArgumentException("A residential or commercial job type is required.");
        if (input.Items is null || input.Openings is null) throw new ArgumentException("Items and openings arrays are required.");
        if (input.Items.Count > 100 || input.LaborHours is < 0 or > 1000 || input.DiscountPercent is < 0 or > 100)
            throw new ArgumentException("Supply up to 100 items or labor, non-negative labor hours, and a discount between 0 and 100%.");
        if (input.Openings.Count > 100 || JsonSerializer.SerializeToUtf8Bytes(input.Openings).Length > 200_000 || input.Openings.Any(opening =>
            string.IsNullOrWhiteSpace(opening.Name) || opening.Name.Length > 200 || opening.Id.Length > 100 || opening.Service.Length > 200 || opening.Handing.Length > 100 ||
            opening.Notes.Length > 4000 || opening.CommercialNotes.Length > 4000 || opening.Measurements.Count > 50 ||
            opening.Measurements.Any(pair => pair.Key.Length > 100 || pair.Value.Value.Length > 100 || pair.Value.Certainty is not ("measured" or "estimated" or "unknown"))))
            throw new ArgumentException("Opening observations exceed the supported size or contain invalid measurement certainty.");
        if (input.Openings.Count > 0 && input.Items.Any(item => !input.Openings.Any(opening => opening.Name == item.OpeningName)))
            throw new ArgumentException("Each item must reference a named opening in the observation record.");
        var context = Context(document, [input.JobType]);
        var standardHours = input.Openings.Sum(opening => context.LaborHoursByJobType[input.JobType].GetValueOrDefault(opening.Service, 0m));
        var laborHours = input.LaborHours == 0 ? standardHours : input.LaborHours;
        var overrideReason = input.LaborOverrideReason?.Trim();
        if (laborHours < standardHours) throw new ArgumentException("Field labor hours cannot be less than the configured standard.");
        if (standardHours > 0 && laborHours > standardHours && (string.IsNullOrWhiteSpace(overrideReason) || overrideReason.Length > 500))
            throw new ArgumentException("Explain the additional labor hours in 500 characters or fewer.");
        if (input.Items.Count == 0 && laborHours <= 0) throw new ArgumentException("Select a catalog item or configure labor hours for this service.");
        if (context.TaxPercent is null || context.TaxPercent is < 0 or > 100) throw new ArgumentException("Configure the tax percentage before generating a quote (zero is allowed).");
        if (context.LaborRatePerHour is < 0 or > 1_000_000) throw new ArgumentException("Hourly labor rate is outside the supported range.");
        if (laborHours > 0 && context.LaborRatePerHour is null) throw new ArgumentException("Configure the hourly labor rate before quoting labor.");
        var lines = input.Items.Select(item => {
            if (item.Quantity is <= 0 or > 10000 || string.IsNullOrWhiteSpace(item.OpeningName) || item.OpeningName.Length > 200) throw new ArgumentException("Each quote item requires an opening name and a positive quantity no greater than 10,000.");
            var matches = context.Catalog.Where(product => product.Id == item.CatalogItemId).ToArray();
            if (matches.Length != 1) throw new ArgumentException($"Catalog item {item.CatalogItemId} is missing, sample-only, ambiguous, or unavailable for {input.JobType} work. Configure an authoritative catalog price.");
            var product = matches[0];
            return new LocksmithPricedLineDto { CatalogItemId = product.Id, Name = product.Name, OpeningName = item.OpeningName.Trim(), Quantity = item.Quantity, UnitPrice = product.UnitPrice, Total = Money(product.UnitPrice * item.Quantity) };
        }).ToList();
        var subtotal = lines.Sum(line => line.Total) + Money(laborHours * (context.LaborRatePerHour ?? 0));
        var discount = Money(subtotal * input.DiscountPercent / 100);
        var tax = Money((subtotal - discount) * context.TaxPercent.Value / 100);
        var snapshot = new LocksmithPricingSnapshotDto { JobType = input.JobType, PolicyVersion = context.PolicyVersion, Lines = lines, Openings = input.Openings, LaborHours = laborHours, StandardLaborHours = standardHours, LaborOverrideReason = laborHours > standardHours ? overrideReason : null,
            LaborRatePerHour = context.LaborRatePerHour ?? 0, DiscountPercent = input.DiscountPercent, DiscountAmount = discount, TaxPercent = context.TaxPercent.Value,
            TaxAmount = tax, Subtotal = subtotal, Total = subtotal - discount + tax };
        using var json = JsonDocument.Parse(document.ValuesJson);
        var policy = json.RootElement.GetProperty("locksmith").GetProperty("pricing");
        if (policy.GetProperty("requireOfficeApproval").GetBoolean()) snapshot.ApprovalReasons.Add("Office approval is required for every quote.");
        if (input.DiscountPercent > policy.GetProperty("maxDiscountPercent").GetDecimal()) snapshot.ApprovalReasons.Add("The requested discount exceeds the technician discount limit.");
        var limit = Number(policy, "approvalAboveTotal");
        if (limit.HasValue && snapshot.Total > limit.Value) snapshot.ApprovalReasons.Add("The quote total exceeds the office approval threshold.");
        return snapshot;
    }
    private static decimal? Number(JsonElement value, string key) => value.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetDecimal(out var number) ? number : null;
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
