using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Entities
{
    public class PatientNote : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;  // TenantId|PatientId
        public string RowKey { get; set; } = default!;        // noteId_ticks_recordId

        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid AuthorId { get; set; }
        public Guid? NoteTypeId { get; set; }
        public Guid? NoteTypeProfileId { get; set; }
        public string NoteBody { get; set; } = string.Empty;
        public NoteCategory Category { get; set; } = default!;
        public NoteVisibility Visibility { get; set; } = default!;
        public string Tags { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
