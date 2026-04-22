using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class ProcessingTokenLedger : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = default;
        public DateTimeOffset? Timestamp { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid MessageId { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public int TokensCredited { get; set; }
        public int TokensDebited { get; set; }
        public string TokenType { get; set; } = default!;
        public string Description { get; set; } = default!;
        public DateTime RequestedAt { get; set; }
        public bool Completed { get; set; }
    }
}
