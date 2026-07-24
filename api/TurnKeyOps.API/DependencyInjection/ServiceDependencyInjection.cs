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
        public static IServiceCollection AddManagedServices(this IServiceCollection services, bool enableServiceBus = true)
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
            services.AddScoped<IInviteService, InviteService>();
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
            services.AddScoped<StripePaymentProvider>();
            services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
            services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<PayPalPaymentProvider>());
            services.AddScoped<IBillingAdminService, BillingAdminService>();
            services.AddScoped<ITenantBillingAccountService, TenantBillingAccountService>();
            services.AddScoped<ITenantSubscriptionService, TenantSubscriptionService>();
            services.AddScoped<ITenantCreditBalanceService, TenantCreditBalanceService>();
            services.AddScoped<IBillingService, BillingService>();
            services.AddScoped<IBillingEventService, BillingEventService>();
            services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
            if (enableServiceBus)
            {
                services.AddHostedService<BillingEventWorker>();
                services.AddHostedService<CreditUsageWorker>();
            }
            services.AddHostedService<BillingRenewalWorker>();
            services.AddHostedService<MonthlyCreditGrantWorker>();
            return services;
        }
    }
}
