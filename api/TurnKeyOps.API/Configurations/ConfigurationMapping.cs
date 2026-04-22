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

        services.Configure<AzureSpeechSettings>(configuration.GetSection("AzureSpeechSettings"));

        services.Configure<OpenAISettings>(configuration.GetSection("OpenAISettings"));

        services.Configure<SummarizerSettings>(configuration.GetSection("SummarizerSettings"));
        services.Configure<PatientClinicalSummarySettings>(configuration.GetSection("PatientClinicalSummarySettings"));
        services.Configure<AppointmentDataCompletenessSettings>(configuration.GetSection("AppointmentDataCompleteness"));

        services.Configure<SummarizerPromptTemplates>(configuration.GetSection("SummarizerPromptTemplates"));

        services.Configure<ApiErrorHandlingOptions>(configuration.GetSection("ApiErrorHandling"));
        services.Configure<DiagnosisCodeCacheSettings>(configuration.GetSection("DiagnosisCodeCache"));

        return services;
    }
}
