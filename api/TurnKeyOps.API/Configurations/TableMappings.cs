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





