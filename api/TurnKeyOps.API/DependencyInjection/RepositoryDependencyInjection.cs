using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Repositories;

namespace MedInsights.API.DependencyInjection
{
    public static class RepositoryDependencyInjection
    {
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {

            services.AddSingleton<ISystemErrorRepository, SystemErrorRepository>();

            services.AddScoped<IActivityEntryRepository, ActivityEntryRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<ActivityItems>>(sp => sp.GetRequiredService<IActivityEntryRepository>());
            services.AddScoped<IActivityItemDefinitionRepository, ActivityItemDefinitionRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<ActivityItemDefinition>>(sp => sp.GetRequiredService<IActivityItemDefinitionRepository>());
            services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<ActivityLog>>(sp => sp.GetRequiredService<IActivityLogRepository>());
            
            services.AddScoped<ITokenLedgerRepository, TokenLedgerRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TokenLedger>>(sp => sp.GetRequiredService<ITokenLedgerRepository>());

            services.AddScoped<IProcessingTokenLedgerRepository, ProcessingTokenLedgerRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<ProcessingTokenLedger>>(sp => sp.GetRequiredService<IProcessingTokenLedgerRepository>());
            services.AddScoped<IProcessingCreditUsageRepository, ProcessingCreditUsageRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<ProcessingCreditUsage>>(sp => sp.GetRequiredService<IProcessingCreditUsageRepository>());

            services.AddScoped<IUserProfileRepository, UserProfileRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<UserProfile>>(sp => sp.GetRequiredService<IUserProfileRepository>());
            services.AddScoped<IUserContactChangeRequestRepository, UserContactChangeRequestRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<UserContactChangeRequest>>(sp => sp.GetRequiredService<IUserContactChangeRequestRepository>());

            services.AddScoped<ITenantProfileRepository, TenantProfileRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantProfile>>(sp => sp.GetRequiredService<ITenantProfileRepository>());

            services.AddScoped<ITenantOnboardingPolicyRepository, TenantOnboardingPolicyRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantOnboardingPolicy>>(sp => sp.GetRequiredService<ITenantOnboardingPolicyRepository>());

            services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PlatformUser>>(sp => sp.GetRequiredService<IPlatformUserRepository>());

            services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantMembership>>(sp => sp.GetRequiredService<ITenantMembershipRepository>());
            services.AddScoped<ITenantRoleDefinitionRepository, TenantRoleDefinitionRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantRoleDefinition>>(sp => sp.GetRequiredService<ITenantRoleDefinitionRepository>());
            services.AddScoped<IRolePermissionMappingRepository, RolePermissionMappingRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<RolePermissionMapping>>(sp => sp.GetRequiredService<IRolePermissionMappingRepository>());

            services.AddScoped<IInviteRepository, InviteRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<Invite>>(sp => sp.GetRequiredService<IInviteRepository>());

            services.AddScoped<ITenantBillingAccountRepository, TenantBillingAccountRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantBillingAccount>>(sp => sp.GetRequiredService<ITenantBillingAccountRepository>());

            services.AddScoped<ITenantSubscriptionRepository, TenantSubscriptionRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantSubscription>>(sp => sp.GetRequiredService<ITenantSubscriptionRepository>());

            services.AddScoped<ITenantSeatEntitlementRepository, TenantSeatEntitlementRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantSeatEntitlement>>(sp => sp.GetRequiredService<ITenantSeatEntitlementRepository>());

            services.AddScoped<IPricingRuleSnapshotRepository, PricingRuleSnapshotRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PricingRuleSnapshot>>(sp => sp.GetRequiredService<IPricingRuleSnapshotRepository>());

            services.AddScoped<ITenantCreditBalanceRepository, TenantCreditBalanceRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<TenantCreditBalance>>(sp => sp.GetRequiredService<ITenantCreditBalanceRepository>());

            services.AddScoped<IUserCreditPeriodRepository, UserCreditPeriodRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<UserCreditPeriod>>(sp => sp.GetRequiredService<IUserCreditPeriodRepository>());

            services.AddScoped<ICreditLedgerRepository, CreditLedgerRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<CreditLedger>>(sp => sp.GetRequiredService<ICreditLedgerRepository>());

            services.AddScoped<IBillingLedgerRepository, BillingLedgerRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<BillingLedger>>(sp => sp.GetRequiredService<IBillingLedgerRepository>());

            services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<WebhookEvent>>(sp => sp.GetRequiredService<IWebhookEventRepository>());

            services.AddScoped<IAuditEventRepository, AuditEventRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<AuditEvent>>(sp => sp.GetRequiredService<IAuditEventRepository>());

            services.AddScoped<IOperationalAlertRepository, OperationalAlertRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<OperationalAlert>>(sp => sp.GetRequiredService<IOperationalAlertRepository>());

            services.AddScoped<IChatRepository, ChatRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<Chat>>(sp => sp.GetRequiredService<IChatRepository>());
            
            services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<ChatMessage>>(sp => sp.GetRequiredService<IChatMessageRepository>());

            return services;
        }

    }
}






