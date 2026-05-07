using MedInsights.Services;
using MedInsights.Repositories.Interfaces;
using MedInsights.Lib;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Events;
using MedInsights.Services.BackgroundServices;
using MedInsights.AzureServices.Interfaces;
using MedInsights.AzureServices;
using MedInsights.API.Infrastructure;

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

            // Capture Services
            services.AddScoped<ICaptureDraftNoteService, CaptureDraftNoteService>();

            // OpenAI / Azure Speech
            services.AddScoped<IAIService<OpenAI.Chat.ChatMessage>, OpenAIService>();
            services.AddSingleton<IAzureSpeechService, AzureSpeechService>();
            //services.AddScoped<IOpenAIRealtimeService, OpenAIRealtimeService>();
            
            //Chats
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IChatOrchestratorService, ChatOrchestratorService>();
            //services.AddScoped<IChatSummarizerService, ChatSummarizerService>();
            services.AddScoped<IChatTitleService, ChatTitleService>();
            services.AddScoped<IChatSummaryService, ChatSummaryService>();
            //services.AddScoped<IChatPostProcessor, ChatPostProcessor>();

            // Audio Capture Services
            services.AddScoped<IAudioCaptureService, AudioCaptureService>();
    
            //services.AddScoped<IDictationService, DictationService>();
            services.AddScoped<IPatientEncounterService, PatientEncounterService>();
            services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();

            // Document Services
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<IFileTextExtractorService, FileTextExtractorService>();

            // Patient Services            
            services.AddScoped<IPatientService, PatientService>();
            services.AddScoped<IPatientAppointmentService, PatientAppointmentService>();
            services.AddScoped<IAppointmentTypeService, AppointmentTypeService>();
            services.AddScoped<IAppointmentTypeProvisioningService, AppointmentTypeProvisioningService>();
            services.AddScoped<IStartupSeedContributor, AppointmentTypeStartupSeedContributor>();
            services.AddScoped<IFacilityService, FacilityService>();
            services.AddScoped<IStartupSeeder, StartupSeeder>();
            services.AddScoped<IStartupSeedContributor, SystemDefaultsSeedContributor>();
            services.AddScoped<INoteTypeService, NoteTypeService>();
            services.AddScoped<INoteTypeProfileService, NoteTypeProfileService>();
            services.AddScoped<INoteTypePromptBuilderService, NoteTypePromptBuilderService>();
            services.AddScoped<IPatientContactService, PatientContactService>();
            services.AddScoped<IPatientOrderService, PatientOrderService>();
            services.AddScoped<IPatientMedicationService, PatientMedicationService>();
            services.AddScoped<IPatientDiagnosisService, PatientDiagnosisService>();
            services.AddScoped<IDiagnosisCodeService, DiagnosisCodeService>();
            services.AddScoped<IPatientInsuranceService, PatientInsuranceService>();
            services.AddScoped<IPatientAllergyService, PatientAllergyService>();
            services.AddScoped<IPatientLabsService, PatientLabsService>();
            services.AddScoped<IPatientEnvironmentalHistoryService, PatientEnvironmentalHistoryService>();
            services.AddScoped<IPatientMaritalHistoryService, PatientMaritalHistoryService>();
            services.AddScoped<IPatientMilitaryFirstResponderService, PatientMilitaryFirstResponderService>();
            services.AddScoped<IPatientFamilyMedicalHistoryService, PatientFamilyMedicalHistoryService>();
            services.AddScoped<IPatientContextService, PatientContextService>();
            services.AddScoped<IPatientBillingNoteService, PatientBillingNoteService>();
            services.AddScoped<IPatientNoteService, PatientNoteService>();
            services.AddScoped<IPatientVitalsService, PatientVitalsService>();
            services.AddScoped<IPatientReferralService, PatientReferralService>();
            services.AddScoped<IPatientReferralActivityService, PatientReferralActivityService>();
            services.AddScoped<IReferralWorkItemService, ReferralWorkItemService>();
            services.AddScoped<IPatientClinicalSummaryService, PatientClinicalSummaryService>();

            // Prompt Templates
            services.AddScoped<IPromptTemplateService, PromptTemplateService>();

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
            services.AddHostedService<StartupCacheLoader>();

            return services;
        }
    }
}
