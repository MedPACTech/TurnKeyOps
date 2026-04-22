using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class AuditEvent : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? Source { get; set; }
        public string? Description { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime OccurredUtc { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
