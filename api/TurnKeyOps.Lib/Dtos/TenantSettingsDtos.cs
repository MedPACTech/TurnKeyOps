using System.Text.Json;

namespace TurnKeyOps.Lib.Dtos;

public static class TenantSettingKinds
{
    public const string PublicContent = "public-content";
    public const string Billing = "billing";
    public const string Operational = "operational";
    public const string Brand = "brand";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PublicContent,
        Billing,
        Operational,
        Brand
    };
}

public sealed class TenantSettingsDocumentDto
{
    public string Kind { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public bool IsPublic { get; set; }
    public JsonElement Values { get; set; }
    public IReadOnlyList<string> ConfiguredSecretKeys { get; set; } = [];
    public string Version { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
}

public sealed class UpdateTenantSettingsDocumentDto
{
    public int SchemaVersion { get; set; } = 1;
    public JsonElement Values { get; set; }
    public Dictionary<string, string> SecretReferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ExpectedVersion { get; set; }
}

public sealed class ContactAccessGrantDto
{
    public string ContactId { get; set; } = string.Empty;
    public string Role { get; set; } = "none";
    public bool Enabled { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
}

public sealed class UpdateContactAccessGrantDto
{
    public string Role { get; set; } = "none";
    public bool Enabled { get; set; } = true;
    public string? ExpectedVersion { get; set; }
}
