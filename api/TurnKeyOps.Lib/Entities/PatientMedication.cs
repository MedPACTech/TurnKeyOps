using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientMedication : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public Guid ProviderId { get; set; }
        public Guid PrescriberId { get; set; }
        public string PrescriberName { get; set; } = string.Empty;
        public DateTime DateNoted { get; set; }
        public Guid MedicationId { get; set; }
        public string Medication { get; set; } = string.Empty;
        public string Strength { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public bool IsEnded { get; set; }
        public DateTime DateEnded { get; set; }
        public bool IsLocked { get; set; }
    }
}
