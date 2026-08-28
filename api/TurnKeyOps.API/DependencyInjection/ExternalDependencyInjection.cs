using MedInsights.Lib.Configurations;
using MedInsights.Services;
using Microsoft.Extensions.Options;
using OpenAI;
using Stripe;

namespace MedInsights.API.DependencyInjection
{
    public static class ExternalDependencyInjection
    {
        public static IServiceCollection AddExternalClients(this IServiceCollection services, IConfiguration configuration)
        {
            var enabledProviders = configuration
                .GetSection($"{BillingIntegrationOptions.SectionName}:EnabledProviders")
                .Get<string[]>() ?? [];

            if (enabledProviders.Contains("Stripe", StringComparer.OrdinalIgnoreCase))
            {
                services.AddSingleton(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<StripeSettings>>().Value;

                    if (string.IsNullOrWhiteSpace(opts.SecretKey))
                        throw new InvalidOperationException("Stripe secret key missing (StripeSettings:SecretKey).");

                    return new StripeClient(opts.SecretKey);
                });
            }

            services.AddHttpClient<PayPalPaymentProvider>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<PayPalSettings>>().Value;
                var integrationOptions = sp.GetRequiredService<IOptions<BillingIntegrationOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                    client.BaseAddress = new Uri(opts.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(Math.Clamp(integrationOptions.RequestTimeoutSeconds, 5, 120));
            });

            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<OpenAISettings>>().Value;

                if (string.IsNullOrWhiteSpace(opts.Key))
                    throw new InvalidOperationException("OpenAI API key missing (OpenAISettings:Key).");

                return new OpenAIClient(opts.Key);
            });

            return services;
        }
    }
}
