using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class WebhookEvent : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string ProcessingStatus { get; set; } = string.Empty;
        public Guid? CorrelationTenantId { get; set; }
        public string? PayloadHash { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public DateTime? ProcessedUtc { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
