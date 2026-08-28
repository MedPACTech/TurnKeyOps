namespace MedInsights.Lib.Configurations;

public sealed class BillingIntegrationOptions
{
    public const string SectionName = "BillingIntegrations";

    public string DefaultProvider { get; set; } = string.Empty;
    public string[] EnabledProviders { get; set; } = [];
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 2;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
