using MedInsights.Lib.Configurations;

namespace MedInsights.API.Configurations;

public static class ProductionIntegrationConfiguration
{
    private static readonly HashSet<string> SupportedBillingProviders =
        new(StringComparer.OrdinalIgnoreCase) { "Stripe", "PayPal" };

    public static void Validate(IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
            return;

        var failures = new List<string>();
        ValidateCommunications(configuration, failures);
        ValidateBilling(configuration, failures);

        if (!string.Equals(
                configuration["ProductionIntegrations:SecretsSource"]?.Trim(),
                "KeyVault",
                StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("ProductionIntegrations:SecretsSource must explicitly be KeyVault");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Production integration configuration is invalid: {string.Join(", ", failures)}.");
        }
    }

    private static void ValidateCommunications(IConfiguration configuration, ICollection<string> failures)
    {
        Require(configuration, "IBeam:Communications:Email:FromAddress", "ACS email sender", failures);
        Require(configuration, "IBeam:Communications:Email:Providers:AzureCommunications:ConnectionString", "ACS email connection", failures);
        Require(configuration, "IBeam:Communications:Sms:FromPhoneNumber", "ACS SMS sender", failures);
        Require(configuration, "IBeam:Communications:Sms:Providers:AzureCommunications:ConnectionString", "ACS SMS connection", failures);

        foreach (var tenantKey in new[] { "bdr", "thinkpink" })
        {
            var path = $"ProductionIntegrations:Communications:Tenants:{tenantKey}";
            Require(configuration, $"{path}:TenantId", $"{tenantKey} communication tenant id", failures);
            Require(configuration, $"{path}:EmailFromAddress", $"{tenantKey} email sender", failures);
            Require(configuration, $"{path}:SmsFromPhoneNumber", $"{tenantKey} SMS sender", failures);

            var tenantId = configuration[$"{path}:TenantId"];
            if (!string.IsNullOrWhiteSpace(tenantId) &&
                (!Guid.TryParse(tenantId, out var parsedTenantId) || parsedTenantId == Guid.Empty))
            {
                failures.Add($"{tenantKey} communication tenant id must be a non-empty GUID");
            }
        }
    }

    private static void ValidateBilling(IConfiguration configuration, ICollection<string> failures)
    {
        var section = BillingIntegrationOptions.SectionName;
        var enabledProviders = configuration.GetSection($"{section}:EnabledProviders").Get<string[]>() ?? [];
        var defaultProvider = configuration[$"{section}:DefaultProvider"]?.Trim();

        if (enabledProviders.Length == 0)
            failures.Add("at least one production billing provider");

        var unsupported = enabledProviders
            .Where(provider => !SupportedBillingProviders.Contains(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unsupported.Length > 0)
            failures.Add($"unsupported billing providers ({string.Join(", ", unsupported)})");

        if (string.IsNullOrWhiteSpace(defaultProvider) ||
            !enabledProviders.Contains(defaultProvider, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add("an enabled default billing provider");
        }

        if (enabledProviders.Contains("Stripe", StringComparer.OrdinalIgnoreCase))
        {
            Require(configuration, "StripeSettings:SecretKey", "Stripe secret key", failures);
            Require(configuration, "StripeSettings:WebhookSecret", "Stripe webhook secret", failures);
            ValidateCatalog(configuration.GetSection("StripeBillingCatalogSettings:SubscriptionPriceMap"), "Stripe subscription catalog", failures);
            ValidateCatalog(configuration.GetSection("StripeBillingCatalogSettings:TopUpPriceMap"), "Stripe top-up catalog", failures);
        }

        if (enabledProviders.Contains("PayPal", StringComparer.OrdinalIgnoreCase))
        {
            Require(configuration, "PayPalSettings:ClientId", "PayPal client id", failures);
            Require(configuration, "PayPalSettings:ClientSecret", "PayPal client secret", failures);
            Require(configuration, "PayPalSettings:WebhookId", "PayPal webhook id", failures);
            ValidateCatalog(configuration.GetSection("PayPalBillingCatalogSettings:SubscriptionPlanMap"), "PayPal subscription catalog", failures);

            var baseUrl = configuration["PayPalSettings:BaseUrl"]?.Trim();
            if (!string.Equals(baseUrl, "https://api-m.paypal.com", StringComparison.OrdinalIgnoreCase))
                failures.Add("PayPal production API base URL");
        }
    }

    private static void Require(
        IConfiguration configuration,
        string key,
        string label,
        ICollection<string> failures)
    {
        var value = configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value) || IsPlaceholder(value))
            failures.Add(label);
    }

    private static void ValidateCatalog(
        IConfigurationSection section,
        string label,
        ICollection<string> failures)
    {
        var values = section.GetChildren()
            .Select(item => item.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (values.Length == 0 || values.Any(value => IsPlaceholder(value!)))
            failures.Add(label);
    }

    private static bool IsPlaceholder(string value) =>
        value.Contains("your-", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("<required>", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("changeme", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("price_456", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("price_789", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("price_topup_", StringComparison.OrdinalIgnoreCase);
}
