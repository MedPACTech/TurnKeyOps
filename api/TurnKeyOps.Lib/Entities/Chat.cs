using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class Chat : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public string Title { get; set; } = "";
        public string CustomTitle { get; set; } = "";
        public int TokensUsed { get; set; }
        public Guid? PatientId { get; set; } = default!;
        public DateTime DateChatCreated { get; set; }
        public DateTime DateChatUpdated { get; set; }
        public string ChatSummary { get; set; } = "";
        public string ChatMetadata { get; set; } = "";
        public string AttachedDocuments { get; set; } = "";
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
