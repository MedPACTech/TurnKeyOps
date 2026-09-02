using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Utils;

namespace MedInsights.API.Configurations;

public static class ConfigurationMapping
{
    public static IServiceCollection AddAppConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RepositoryKeySettings>(configuration.GetSection("RepositoryKeySettings"));

        // Keep local key helpers aligned with IBeam Azure Tables key format configuration.
        var guidKeyFormat =
            configuration["IBeam:Repositories:AzureTables:GuidKeyFormat"]
            ?? configuration["RepositoryKeySettings:GuidFormat"];
        RepositoryKeyHelper.ConfigureGuidKeyFormat(guidKeyFormat);

        services.Configure<SystemSettings>(configuration.GetSection("SystemSettings"));

        services.Configure<AzureStorageSettings>(configuration.GetSection("AzureStorageSettings"));

        services.Configure<StripeSettings>(configuration.GetSection("StripeSettings"));
        services.Configure<StripeBillingCatalogSettings>(configuration.GetSection("StripeBillingCatalogSettings"));
        services.Configure<PayPalSettings>(configuration.GetSection("PayPalSettings"));
        services.Configure<PayPalBillingCatalogSettings>(configuration.GetSection("PayPalBillingCatalogSettings"));
        services.Configure<BillingIntegrationOptions>(
            configuration.GetSection(BillingIntegrationOptions.SectionName));
        services.Configure<ProductionCommunicationOptions>(
            configuration.GetSection(ProductionCommunicationOptions.SectionName));

        services.Configure<AzureSpeechSettings>(configuration.GetSection("AzureSpeechSettings"));

        services.Configure<OpenAISettings>(configuration.GetSection("OpenAISettings"));

        services.Configure<ApiErrorHandlingOptions>(configuration.GetSection("ApiErrorHandling"));

        return services;
    }
}
