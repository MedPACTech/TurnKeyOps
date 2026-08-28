namespace MedInsights.Lib.Configurations;

public sealed class UserAdministrationOptions
{
    public const string SectionName = "UserAdministration";

    public Dictionary<string, ManagedTenantDefinition> Tenants { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ManagedTenantDefinition
{
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
