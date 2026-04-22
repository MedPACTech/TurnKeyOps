using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientMaritalHistory : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public DateTime DateNoted { get; set; } = DateTime.UtcNow;
        public string MaritalStatus { get; set; } = string.Empty;
        public DateOnly? DateMarried { get; set; }
        public DateOnly? DateDivorced { get; set; }
        public DateOnly? DateWidowed { get; set; }
        public string SpouseName { get; set; } = string.Empty;
        public string HasChildren { get; set; } = string.Empty;
        public int? NumberOfChildren { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
