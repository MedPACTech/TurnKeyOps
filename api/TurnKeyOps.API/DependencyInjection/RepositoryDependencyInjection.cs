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
            
            // Capture Repository
            services.AddScoped<ICaptureDraftNoteRepository, CaptureDraftNoteRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<CaptureDraftNote>>(sp => sp.GetRequiredService<ICaptureDraftNoteRepository>());


            services.AddScoped<IDictationRepository, DictationRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<Dictation>>(sp => sp.GetRequiredService<IDictationRepository>());

            services.AddScoped<IPatientEncounterRepository, PatientEncounterRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientEncounter>>(sp => sp.GetRequiredService<IPatientEncounterRepository>());

            services.AddScoped<IPatientBillingNoteRepository, PatientBillingNoteRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientBillingNote>>(sp => sp.GetRequiredService<IPatientBillingNoteRepository>());

            services.AddScoped<IPatientReferralRepository, PatientReferralRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientReferral>>(sp => sp.GetRequiredService<IPatientReferralRepository>());
            services.AddScoped<IPatientReferralActivityRepository, PatientReferralActivityRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientReferralActivity>>(sp => sp.GetRequiredService<IPatientReferralActivityRepository>());
            services.AddScoped<IReferralWorkItemRepository, ReferralWorkItemRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<ReferralWorkItem>>(sp => sp.GetRequiredService<IReferralWorkItemRepository>());

            services.AddScoped<IPatientAppointmentRepository, PatientAppointmentRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientAppointment>>(sp => sp.GetRequiredService<IPatientAppointmentRepository>());
            services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<AppointmentTypeDefinition>>(sp => sp.GetRequiredService<IAppointmentTypeRepository>());

            services.AddScoped<IFacilityRepository, FacilityRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<Facility>>(sp => sp.GetRequiredService<IFacilityRepository>());
            services.AddScoped<IFacilityPatientAssignmentRepository, FacilityPatientAssignmentRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<FacilityPatientAssignment>>(sp => sp.GetRequiredService<IFacilityPatientAssignmentRepository>());

            services.AddScoped<INoteTypeRepository, NoteTypeRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<NoteType>>(sp => sp.GetRequiredService<INoteTypeRepository>());
            services.AddScoped<INoteTypeProfileRepository, NoteTypeProfileRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<NoteTypeProfile>>(sp => sp.GetRequiredService<INoteTypeProfileRepository>());

            services.AddScoped<IAudioCaptureRepository, AudioCaptureRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<AudioCapture>>(sp => sp.GetRequiredService<IAudioCaptureRepository>());

            services.AddScoped<IPatientContactRepository, PatientContactRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientContact>>(sp => sp.GetRequiredService<IPatientContactRepository>());

            services.AddScoped<IPatientOrderRepository, PatientOrderRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientOrder>>(sp => sp.GetRequiredService<IPatientOrderRepository>());

            services.AddScoped<IPatientMedicationRepository, PatientMedicationRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientMedication>>(sp => sp.GetRequiredService<IPatientMedicationRepository>());

            services.AddScoped<IPatientDiagnosisRepository, PatientDiagnosisRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientDiagnosis>>(sp => sp.GetRequiredService<IPatientDiagnosisRepository>());

            services.AddScoped<IDiagnosisCodeRepository, DiagnosisCodeRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<DiagnosisCode>>(sp => sp.GetRequiredService<IDiagnosisCodeRepository>());

            services.AddScoped<IPatientInsuranceRepository, PatientInsuranceRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientInsurance>>(sp => sp.GetRequiredService<IPatientInsuranceRepository>());

            services.AddScoped<IPatientAllergyRepository, PatientAllergyRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientAllergy>>(sp => sp.GetRequiredService<IPatientAllergyRepository>());

            services.AddScoped<IPatientLabsRepository, PatientLabsRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientLabs>>(sp => sp.GetRequiredService<IPatientLabsRepository>());

            services.AddScoped<IPatientEnvironmentalHistoryRepository, PatientEnvironmentalHistoryRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientEnvironmentalHistory>>(sp => sp.GetRequiredService<IPatientEnvironmentalHistoryRepository>());

            services.AddScoped<IPatientMaritalHistoryRepository, PatientMaritalHistoryRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientMaritalHistory>>(sp => sp.GetRequiredService<IPatientMaritalHistoryRepository>());

            services.AddScoped<IPatientMilitaryFirstResponderRepository, PatientMilitaryFirstResponderRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientMilitaryFirstResponder>>(sp => sp.GetRequiredService<IPatientMilitaryFirstResponderRepository>());

            services.AddScoped<IPatientFamilyMedicalHistoryRepository, PatientFamilyMedicalHistoryRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientFamilyMedicalHistory>>(sp => sp.GetRequiredService<IPatientFamilyMedicalHistoryRepository>());

            services.AddScoped<IPatientContextRepository, PatientContextRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientContext>>(sp => sp.GetRequiredService<IPatientContextRepository>());

            services.AddScoped<IPatientClinicalSummaryCacheRepository, PatientClinicalSummaryCacheRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientClinicalSummaryCache>>(sp => sp.GetRequiredService<IPatientClinicalSummaryCacheRepository>());

            services.AddScoped<IPatientNoteRepository, PatientNoteRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientNote>>(sp => sp.GetRequiredService<IPatientNoteRepository>());

            services.AddScoped<IPatientVitalsRepository, PatientVitalsRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PatientVitals>>(sp => sp.GetRequiredService<IPatientVitalsRepository>());

            services.AddScoped<IPatientRepository, PatientRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<Patient>>(sp => sp.GetRequiredService<IPatientRepository>());

            services.AddScoped<IPromptTemplateRepository, PromptTemplateRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<PromptTemplate>>(sp => sp.GetRequiredService<IPromptTemplateRepository>()); 

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

            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IBeam.Repositories.Abstractions.IBaseRepositoryAsync<Document>>(sp => sp.GetRequiredService<IDocumentRepository>());
            
            return services;
        }

    }
}








