namespace MedInsights.Lib.Configurations;

public sealed class ProductionCommunicationOptions
{
    public const string SectionName = "ProductionIntegrations:Communications";

    public bool Enabled { get; set; } = true;
    public bool UseSharedPlatformSender { get; set; }
    public Dictionary<string, TenantCommunicationProfile> Tenants { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TenantCommunicationProfile
{
    public Guid TenantId { get; set; }
    public string EmailFromAddress { get; set; } = string.Empty;
    public string EmailFromName { get; set; } = string.Empty;
    public string SmsFromPhoneNumber { get; set; } = string.Empty;
}
