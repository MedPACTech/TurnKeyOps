using IBeam.Repositories.AzureTables;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories;

namespace MedInsights.API.Configurations;

public static class AzureTableMappings
{
    public static IServiceCollection AddAzureTableMappings(this IServiceCollection services)
    {        
            services.AddAzureEntityMapping<PatientNote>(o =>
            {
                o.TableName = "PatientNotes";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId),
                    RowKey = EntityKeyPolicy.Row(e.Id)
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientVitals>(o =>
            {
                o.TableName = "PatientVitals";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<Dictation>(o =>
            {
                o.TableName = "Dictations";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientEncounter>(o =>
            {
                o.TableName = "PatientEncounters";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? (Guid.TryParse(e.PatientId, out var patientId)
                            ? EntityKeyPolicy.TenantPatientPartition(tenantId, patientId)
                            : EntityKeyPolicy.TenantPartition(tenantId))
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientBillingNote>(o =>
            {
                o.TableName = "PatientBillingNotes";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? (e.PatientId != Guid.Empty
                            ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                            : EntityKeyPolicy.TenantPartition(tenantId))
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientReferral>(o =>
            {
                o.TableName = "PatientReferrals";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? (e.PatientId != Guid.Empty
                            ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                            : EntityKeyPolicy.TenantPartition(tenantId))
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientReferralActivity>(o =>
            {
                o.TableName = "PatientReferralActivities";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? PatientReferralActivityRepository.PartitionKeyForReferral(tenantId, e.PatientReferralId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey)
                        ? PatientReferralActivityRepository.RowKeyFor(
                            e.CreatedAtUtc == default ? DateTime.UtcNow : e.CreatedAtUtc,
                            e.Id == Guid.Empty ? Guid.NewGuid() : e.Id)
                        : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<ReferralWorkItem>(o =>
            {
                o.TableName = "ReferralWorkItems";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPartition(tenantId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<UserProfile>(o =>
            {
                o.TableName = "UserProfiles";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<UserContactChangeRequest>(o =>
            {
                o.TableName = "UserContactChangeRequests";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? $"USER={e.UserId:N}" : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantProfile>(o =>
            {
                o.TableName = "TenantProfiles";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PlatformUser>(o =>
            {
                o.TableName = "PlatformUsers";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? $"USER={e.Id:N}" : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? "PROFILE" : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantMembership>(o =>
            {
                o.TableName = "TenantMemberships";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantRoleDefinition>(o =>
            {
                o.TableName = "TenantRoleDefinitions";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? TenantRoleDefinitionRepository.SystemPartitionKey : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<RolePermissionMapping>(o =>
            {
                o.TableName = "RolePermissionMappings";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? RolePermissionMappingRepository.SystemPartitionKey : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<Invite>(o =>
            {
                o.TableName = "Invites";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantBillingAccount>(o =>
            {
                o.TableName = "TenantBillingAccounts";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? "BILLING" : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantSubscription>(o =>
            {
                o.TableName = "TenantSubscriptions";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantSeatEntitlement>(o =>
            {
                o.TableName = "TenantSeatEntitlements";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? $"SEATS|{e.SubscriptionId:N}" : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PricingRuleSnapshot>(o =>
            {
                o.TableName = "PricingRuleSnapshots";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantCreditBalance>(o =>
            {
                o.TableName = "TenantCreditBalances";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? "CREDITS" : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<UserCreditPeriod>(o =>
            {
                o.TableName = "UserCreditPeriods";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? $"TENANT={e.TenantId:N}|PERIOD={e.PeriodKey}" : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? $"USER={e.UserId:N}" : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<CreditLedger>(o =>
            {
                o.TableName = "CreditLedger";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? RepositoryKeyHelper.ToOrderedRowKey(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<BillingLedger>(o =>
            {
                o.TableName = "BillingLedger";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? RepositoryKeyHelper.ToOrderedRowKey(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<WebhookEvent>(o =>
            {
                o.TableName = "WebhookEvents";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? e.Provider.ToUpperInvariant() : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<AuditEvent>(o =>
            {
                o.TableName = "AuditEvents";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? (e.TenantId.HasValue ? $"TENANT={e.TenantId.Value:N}" : "GLOBAL") : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? RepositoryKeyHelper.ToOrderedRowKey(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<TenantOnboardingPolicy>(o =>
            {
                o.TableName = "TenantOnboardingPolicies";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? "ONBOARDING" : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<OperationalAlert>(o =>
            {
                o.TableName = "OperationalAlerts";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? (e.TenantId.HasValue ? $"TENANT={e.TenantId.Value:N}" : "GLOBAL") : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? RepositoryKeyHelper.ToOrderedRowKey(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<Chat>(o =>
            {
                o.TableName = "Chats";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPartition(tenantId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<ChatMessage>(o =>
            {
                o.TableName = "ChatMessages";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPartition(tenantId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = false;
            });

            services.AddAzureEntityMapping<TokenLedger>(o =>
            {
                o.TableName = "TokenLedger";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<ActivityItems>(o =>
            {
                o.TableName = "ActivityEntries";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = e.PartitionKey,
                    RowKey = e.RowKey
                };
                o.EnableIdLocator = false;
            });

            services.AddAzureEntityMapping<ActivityItemDefinition>(o =>
            {
                o.TableName = "ItemDefinitions";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = e.PartitionKey,
                    RowKey = e.RowKey
                };
                o.EnableIdLocator = false;
            });

            services.AddAzureEntityMapping<ActivityLog>(o =>
            {
                o.TableName = "ActivityLogs";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = e.PartitionKey,
                    RowKey = e.RowKey
                };
                o.EnableIdLocator = false;
            });

            services.AddAzureEntityMapping<CaptureDraftNote>(o =>
            {
                o.TableName = "CaptureDraftNotes";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? $"TENANT={tenantId:D}|USER={e.ProviderId}"
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<AudioCapture>(o =>
            {
                o.TableName = "AudioCaptures";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPartition(tenantId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<Document>(o =>
            {
                o.TableName = "Documents";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientContext>(o =>
            {
                o.TableName = "PatientContexts";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = e.PartitionKey,
                    RowKey = e.Id != Guid.Empty ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientClinicalSummaryCache>(o =>
            {
                o.TableName = "PatientClinicalSummaries";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId == Guid.Empty ? e.Id : e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey)
                        ? EntityKeyPolicy.Row(e.PatientId == Guid.Empty ? e.Id : e.PatientId)
                        : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<Patient>(o =>
            {
                o.TableName = "Patients";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = EntityKeyPolicy.Row(e.Id)
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientContact>(o =>
            {
                o.TableName = "PatientContacts";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientOrder>(o =>
            {
                o.TableName = "PatientOrders";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPartition(tenantId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientMedication>(o =>
            {
                o.TableName = "PatientMedications";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPartition(tenantId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientDiagnosis>(o =>
            {
                o.TableName = "PatientDiagnoses";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientInsurance>(o =>
            {
                o.TableName = "PatientInsurances";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientFamilyMedicalHistory>(o =>
            {
                o.TableName = "PatientFamilyMedicalHistories";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientAllergy>(o =>
            {
                o.TableName = "PatientAllergies";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientLabs>(o =>
            {
                o.TableName = "PatientLabs";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientEnvironmentalHistory>(o =>
            {
                o.TableName = "PatientEnvironmentalHistories";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientMaritalHistory>(o =>
            {
                o.TableName = "PatientMaritalHistories";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientMilitaryFirstResponder>(o =>
            {
                o.TableName = "PatientMilitaryFirstResponders";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPatientPartition(tenantId, e.PatientId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<PatientAppointment>(o =>
            {
                o.TableName = "PatientAppointments";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<AppointmentTypeDefinition>(o =>
            {
                o.TableName = "AppointmentTypes";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? EntityKeyPolicy.TenantPartition(tenantId)
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<Facility>(o =>
            {
                o.TableName = "Facilities";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<FacilityPatientAssignment>(o =>
            {
                o.TableName = "FacilityPatientAssignments";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = e.PartitionKey,
                    RowKey = e.RowKey
                };
                o.EnableIdLocator = false;
            });

            services.AddAzureEntityMapping<NoteType>(o =>
            {
                o.TableName = "NoteTypes";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? (e.IsSystem ? "NOTETYPE|SYSTEM" : EntityKeyPolicy.TenantPartition(tenantId))
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<NoteTypeProfile>(o =>
            {
                o.TableName = "NoteTypeProfiles";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey)
                        ? (e.IsSystem ? NoteTypeProfileRepository.SystemPartitionKey : EntityKeyPolicy.TenantPartition(tenantId))
                        : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<DiagnosisCode>(o =>
            {
                o.TableName = "ICD10Records";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? "ICD10" : e.PartitionKey,
                    RowKey = !string.IsNullOrWhiteSpace(e.RowKey)
                        ? e.RowKey
                        : (e.Id != Guid.Empty ? EntityKeyPolicy.Row(e.Id) : e.Code)
                };
                o.EnableIdLocator = false;
            });

            services.AddAzureEntityMapping<PromptTemplate>(o =>
            {
                o.TableName = "PromptTemplates";
                o.WriteKey = (_, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? "PromptTemplate" : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = false;
            });

            services.AddAzureEntityMapping<ProcessingTokenLedger>(o =>
            {
                o.TableName = "ProcessingTokenLedger";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            services.AddAzureEntityMapping<ProcessingCreditUsage>(o =>
            {
                o.TableName = "ProcessingCreditUsage";
                o.WriteKey = (tenantId, e) => new AzureEntityKey
                {
                    PartitionKey = string.IsNullOrWhiteSpace(e.PartitionKey) ? EntityKeyPolicy.TenantPartition(tenantId) : e.PartitionKey,
                    RowKey = string.IsNullOrWhiteSpace(e.RowKey) ? EntityKeyPolicy.Row(e.Id) : e.RowKey
                };
                o.EnableIdLocator = true;
            });

            return services;
    }
}



