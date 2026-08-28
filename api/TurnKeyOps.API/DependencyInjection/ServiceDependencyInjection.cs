using MedInsights.Services;
using MedInsights.Repositories.Interfaces;
using MedInsights.Lib;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Events;
using MedInsights.Services.BackgroundServices;
using MedInsights.AzureServices.Interfaces;
using MedInsights.AzureServices;

namespace MedInsights.API.DependencyInjection
{

    public static class ServiceDependencyInjection
    {
        public static IServiceCollection AddManagedServices(
            this IServiceCollection services,
            IConfiguration configuration,
            bool enableServiceBus = true)
        {


            // Activity Log Services
            services.AddScoped<IActivityLogService, ActivityLogService>();

            // Auth Services
           //services.AddScoped<IAuthService, AuthService>();

            // OpenAI / Azure Speech
            services.AddScoped<IAIService<OpenAI.Chat.ChatMessage>, OpenAIService>();
            services.AddSingleton<IAzureSpeechService, AzureSpeechService>();
            //services.AddScoped<IOpenAIRealtimeService, OpenAIRealtimeService>();
            
            services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();

            services.AddScoped<IStartupSeeder, StartupSeeder>();

            // Auth / Tokens
            //services.AddScoped<ITokenService, MedInisightsTokenService>();
            //services.AddScoped<ITokenRevocationStore, DistributedCacheTokenRevocationStore>();

            // Token Ledger
           // services.AddScoped<ITokenLedgerService, TokenLedgerService>();

            // User Profile
            services.AddScoped<IUserProfileService, UserProfileService>();
            services.AddScoped<IUserVerifiedContactService, UserVerifiedContactService>();
            services.AddScoped<ITenantProfileService, TenantProfileService>();
            services.AddScoped<ITenantOnboardingPolicyService, TenantOnboardingPolicyService>();
            services.AddScoped<ITenantRoleService, TenantRoleService>();
            services.AddScoped<ITenantMembershipAuthorizationService, TenantMembershipAuthorizationService>();
            services.AddScoped<ITenantMembershipService, TenantMembershipService>();
            services.AddScoped<ITenantSeatEntitlementService, TenantSeatEntitlementService>();
            services.AddScoped<InviteService>();
            services.AddScoped<IInviteService>(sp => sp.GetRequiredService<InviteService>());
            services.AddScoped<ITrustedTenantInviteService>(sp => sp.GetRequiredService<InviteService>());
            services.AddScoped<IPlatformUserAdministrationService, PlatformUserAdministrationService>();
            services.AddSingleton<ITenantCommunicationProfileResolver, TenantCommunicationProfileResolver>();
            services.AddScoped<ICreditAccountingService, CreditAccountingService>();
            if (enableServiceBus)
            {
                services.AddScoped<ICreditUsageDispatchService, CreditUsageDispatchService>();
            }
            else
            {
                services.AddScoped<ICreditUsageDispatchService, DisabledCreditUsageDispatchService>();
            }
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IOperationalAlertService, OperationalAlertService>();
            if (enableServiceBus)
            {
                services.AddScoped<IBillingEventDispatchService, BillingEventDispatchService>();
            }
            else
            {
                services.AddScoped<IBillingEventDispatchService, DisabledBillingEventDispatchService>();
            }
            services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();
            services.AddSingleton<IProviderHttpExecutor, ProviderHttpExecutor>();
            var billingEnabled = configuration.GetValue<bool>(
                $"{MedInsights.Lib.Configurations.BillingIntegrationOptions.SectionName}:Enabled");
            var enabledProviders = billingEnabled
                ? configuration.GetSection($"{MedInsights.Lib.Configurations.BillingIntegrationOptions.SectionName}:EnabledProviders")
                    .Get<string[]>() ?? []
                : [];
            if (enabledProviders.Contains("Stripe", StringComparer.OrdinalIgnoreCase))
            {
                services.AddScoped<StripePaymentProvider>();
                services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
            }
            if (enabledProviders.Contains("PayPal", StringComparer.OrdinalIgnoreCase))
                services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<PayPalPaymentProvider>());
            services.AddScoped<IBillingAdminService, BillingAdminService>();
            services.AddScoped<ITenantBillingAccountService, TenantBillingAccountService>();
            services.AddScoped<ITenantSubscriptionService, TenantSubscriptionService>();
            services.AddScoped<ITenantCreditBalanceService, TenantCreditBalanceService>();
            services.AddScoped<IBillingService, BillingService>();
            services.AddScoped<IBillingEventService, BillingEventService>();
            services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
            if (enableServiceBus && billingEnabled)
            {
                services.AddHostedService<BillingEventWorker>();
            }
            if (enableServiceBus)
                services.AddHostedService<CreditUsageWorker>();
            if (billingEnabled)
            {
                services.AddHostedService<BillingRenewalWorker>();
                services.AddHostedService<MonthlyCreditGrantWorker>();
            }
            return services;
        }
    }
}
