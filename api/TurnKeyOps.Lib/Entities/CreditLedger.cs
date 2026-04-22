using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class CreditLedger : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string LedgerType { get; set; } = string.Empty;
        public string? SourceBucket { get; set; }
        public int Amount { get; set; }
        public int BalanceAfter { get; set; }
        public string? UsagePeriodKey { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string? SourceReference { get; set; }
        public string? Description { get; set; }
        public DateTime EffectiveUtc { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
