using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class NoteTypeProfile : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }

        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public Guid NoteTypeId { get; set; }
        public string RecordType { get; set; } = default!;
        public string? PromptInstructions { get; set; }
        public string? SectionSchemaJson { get; set; }
        public bool RequireTelehealthAttestation { get; set; }
        public bool RequirePreventiveReview { get; set; }
        public bool IsSystem { get; set; }
        public bool IsDeleted { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
