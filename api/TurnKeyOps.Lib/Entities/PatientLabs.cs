using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientLabs : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public Guid DocumentId { get; set; }
        public string LabType { get; set; } = string.Empty;
        public string LabProvider { get; set; } = string.Empty;
        public DateOnly DateLabCompleted { get; set; }
        public string LabStatus { get; set; } = string.Empty;
        public DateTime? DateUploaded { get; set; }
    }
}
