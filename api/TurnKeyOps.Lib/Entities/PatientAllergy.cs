using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientAllergy : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public string AllergyType { get; set; } = string.Empty;
        public string? Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reaction { get; set; }
        public DateTime DateNoted { get; set; } = DateTime.UtcNow;
    }
}
