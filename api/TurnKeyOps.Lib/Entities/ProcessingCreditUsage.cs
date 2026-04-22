using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class ProcessingCreditUsage : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = default;
        public DateTimeOffset? Timestamp { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid RequestId { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string UsagePeriodKey { get; set; } = string.Empty;
        public int Credits { get; set; }
        public string? SourceReference { get; set; }
        public string? Description { get; set; }
        public DateTime RequestedAtUtc { get; set; }
        public DateTime? EffectiveUtc { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string? LastError { get; set; }
    }
}
