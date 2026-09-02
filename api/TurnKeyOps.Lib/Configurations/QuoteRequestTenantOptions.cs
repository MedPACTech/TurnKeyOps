namespace TurnKeyOps.Lib.Configurations;

public sealed class QuoteRequestTenantOptions
{
    public const string SectionName = "QuoteRequestTenants";

    public Dictionary<string, QuoteRequestTenantDefinition> Tenants { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class QuoteRequestTenantDefinition
{
    public Guid TenantId { get; set; }
    public string DefaultAssignedTo { get; set; } = "Office intake";
    public string DefaultNextAction { get; set; } = "Review request and decide the next office step.";
}
