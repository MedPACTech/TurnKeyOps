using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class NoteType : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid? TenantId { get; set; }
        public Guid? SystemNoteTypeId { get; set; }
        public string RecordType { get; set; } = default!;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NormalizedCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool HasParentNote { get; set; }
        public bool IsSystem { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool? IsDefault { get; set; }
        public int SortOrder { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
