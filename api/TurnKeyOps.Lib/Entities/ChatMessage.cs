using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class ChatMessage : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid TenantId { get; set; }
        public Guid ActorUserId { get; set; }
        public Guid ChatId { get; set; }
        public Guid MessageId { get; set; }
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime ChatTimestamp { get; set; }
        public int TokensUsed { get; set; }
        public string MetadataJson { get; set; } = "{}";
        public string IdempotencyKey { get; set; } = string.Empty;
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
