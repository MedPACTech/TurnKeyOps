using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class TenantSubscription : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }

        [AzureTableProjectedColumn]
        public string Provider { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string PlanCode { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string BillingCadence { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string SubscriptionStatus { get; set; } = string.Empty;

        public string? ProviderSubscriptionId { get; set; }
        public Guid? PricingRuleSnapshotId { get; set; }
        public int CurrentSeatCount { get; set; }
        public int NextRenewalSeatCount { get; set; }
        public bool CancelAtTermEnd { get; set; }
        public DateTime TermStartUtc { get; set; }
        public DateTime TermEndUtc { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
