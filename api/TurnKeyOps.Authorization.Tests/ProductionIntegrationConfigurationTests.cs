using MedInsights.API.Configurations;
using Microsoft.Extensions.Configuration;

namespace MedInsights.Authorization.Tests;

public sealed class ProductionIntegrationConfigurationTests
{
    [Fact]
    public void ProductionRejectsMissingEnabledCommunicationsAndSecretSource()
    {
        var values = new Dictionary<string, string?>
        {
            ["ProductionIntegrations:Communications:Enabled"] = "true"
        };
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionIntegrationConfiguration.Validate(Configuration(values), "Production"));

        Assert.Contains("ACS email sender", exception.Message);
        Assert.Contains("BillingIntegrations:Enabled must be explicitly true or false", exception.Message);
        Assert.Contains("SecretsSource", exception.Message);
    }

    [Fact]
    public void ProductionAcceptsManualLaunchMode()
    {
        var values = SharedLaunchCommunications();

        ProductionIntegrationConfiguration.Validate(Configuration(values), "Production");
    }

    [Fact]
    public void ProductionRejectsProviderConfigurationWhenBillingIsDisabled()
    {
        var values = SharedLaunchCommunications();
        values["BillingIntegrations:DefaultProvider"] = "Stripe";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionIntegrationConfiguration.Validate(Configuration(values), "Production"));

        Assert.Contains("billing is disabled", exception.Message);
    }

    [Fact]
    public void ProductionRejectsSandboxPayPalAndPlaceholderCatalog()
    {
        var values = ValidCommunications();
        values["BillingIntegrations:Enabled"] = "true";
        values["BillingIntegrations:DefaultProvider"] = "PayPal";
        values["BillingIntegrations:EnabledProviders:0"] = "PayPal";
        values["PayPalSettings:ClientId"] = "client";
        values["PayPalSettings:ClientSecret"] = "secret";
        values["PayPalSettings:WebhookId"] = "webhook";
        values["PayPalSettings:BaseUrl"] = "https://api-m.sandbox.paypal.com";
        values["PayPalBillingCatalogSettings:SubscriptionPlanMap:pro"] = "<required>";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionIntegrationConfiguration.Validate(Configuration(values), "Production"));

        Assert.Contains("PayPal production API base URL", exception.Message);
        Assert.Contains("PayPal subscription catalog", exception.Message);
    }

    [Fact]
    public void ProductionAcceptsExplicitStripeConfiguration()
    {
        var values = ValidCommunications();
        values["BillingIntegrations:Enabled"] = "true";
        values["BillingIntegrations:DefaultProvider"] = "Stripe";
        values["BillingIntegrations:EnabledProviders:0"] = "Stripe";
        values["StripeSettings:SecretKey"] = "test-secret-value";
        values["StripeSettings:WebhookSecret"] = "test-webhook-value";
        values["StripeBillingCatalogSettings:SubscriptionPriceMap:pro"] = "price_production_pro";
        values["StripeBillingCatalogSettings:TopUpPriceMap:credits"] = "price_production_credits";

        ProductionIntegrationConfiguration.Validate(Configuration(values), "Production");
    }

    [Fact]
    public void NonProductionDoesNotRequireProductionProviders()
    {
        ProductionIntegrationConfiguration.Validate(Configuration([]), "Development");
    }

    private static Dictionary<string, string?> ValidCommunications() => new()
    {
        ["ProductionIntegrations:SecretsSource"] = "KeyVault",
        ["ProductionIntegrations:Communications:Enabled"] = "true",
        ["IBeam:Communications:Email:FromAddress"] = "noreply@turnkeyops.example",
        ["IBeam:Communications:Email:Providers:AzureCommunications:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=test",
        ["IBeam:Communications:Sms:FromPhoneNumber"] = "+16145550100",
        ["IBeam:Communications:Sms:Providers:AzureCommunications:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=test",
        ["ProductionIntegrations:Communications:Tenants:bdr:TenantId"] = "7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b",
        ["ProductionIntegrations:Communications:Tenants:bdr:EmailFromAddress"] = "noreply@bdr.example",
        ["ProductionIntegrations:Communications:Tenants:bdr:SmsFromPhoneNumber"] = "+16145550101",
        ["ProductionIntegrations:Communications:Tenants:thinkpink:TenantId"] = "88888888-8888-8888-8888-888888888882",
        ["ProductionIntegrations:Communications:Tenants:thinkpink:EmailFromAddress"] = "noreply@thinkpink.example",
        ["ProductionIntegrations:Communications:Tenants:thinkpink:SmsFromPhoneNumber"] = "+16145550102"
    };

    private static Dictionary<string, string?> SharedLaunchCommunications() => new()
    {
        ["ProductionIntegrations:SecretsSource"] = "AppServiceSettings",
        ["ProductionIntegrations:Communications:Enabled"] = "true",
        ["ProductionIntegrations:Communications:UseSharedPlatformSender"] = "true",
        ["IBeam:Communications:Email:FromAddress"] = "noreply@turnkeyops.example",
        ["IBeam:Communications:Email:Providers:AzureCommunications:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=test",
        ["IBeam:Communications:Sms:FromPhoneNumber"] = "+16145550100",
        ["IBeam:Communications:Sms:Providers:AzureCommunications:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=test",
        ["BillingIntegrations:Enabled"] = "false"
    };

    private static IConfiguration Configuration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
