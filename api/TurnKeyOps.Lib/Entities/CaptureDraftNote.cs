using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class CaptureDraftNote : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        public Guid Id { get; set; }

        public string? PatientId { get; set; } = null;
        public string ProviderId { get; set; } = default!;

        public string CaptureSourceType { get; set; } = string.Empty;
        public string? CaptureSourceId { get; set; } = null;
        public string CaptureSourceText { get; set; } = string.Empty;
        public string CaptureSourceAddendum { get; set; } = string.Empty;

        public string CaptureStatus { get; set; } = string.Empty;

        public string NoteType { get; set; } = default!;
        public string NoteTitle { get; set; } = string.Empty;

        public string NoteBody { get; set; } = string.Empty;
        public string BillingBody { get; set; } = string.Empty;
        public string CommunicationBody { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public Guid CreatedBy { get; set; }
        public string Tags { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
