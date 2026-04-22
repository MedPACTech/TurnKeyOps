using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class PatientBillingNote : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        public Guid Id { get; set; }
        [AzureTableProjectedColumn]
        public Guid? EncounterId { get; set; }
        [AzureTableProjectedColumn]
        public Guid CaptureDraftNoteId { get; set; }
        [AzureTableProjectedColumn]
        public Guid PatientId { get; set; }
        public Guid ProviderId { get; set; }
        public string NoteType { get; set; } = string.Empty;
        public string NoteTitle { get; set; } = string.Empty;
        public string BillingBody { get; set; } = string.Empty;
        public DateTime DateSigned { get; set; }
        public Guid SignedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}

