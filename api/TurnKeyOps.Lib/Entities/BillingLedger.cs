using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class BillingLedger : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string? ProviderEventId { get; set; }
        public string? ProviderInvoiceId { get; set; }
        public string? ProviderPaymentIntentId { get; set; }
        public string? ProviderSubscriptionId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? Description { get; set; }
        public DateTime EffectiveUtc { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
