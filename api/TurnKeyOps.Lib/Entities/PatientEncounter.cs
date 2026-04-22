using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class PatientEncounter : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        [AzureTableProjectedColumn]
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        [AzureTableProjectedColumn]
        public string? PatientId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string EncounterBody { get; set; } = string.Empty;
        
        [AzureTableProjectedColumn]
        public Guid CaptureDraftNoteId { get; set; }
        
        [AzureTableProjectedColumn]
        public Guid ProviderId { get; set; }
        
        [AzureTableProjectedColumn]
        public string NoteType { get; set; } = string.Empty;
        public string NoteTitle { get; set; } = string.Empty;
        public string Data { get; set; } = "{}";
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
