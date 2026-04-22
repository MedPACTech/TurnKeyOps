using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientDiagnosis : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public Guid DiagnosisCodeId { get; set; }
        public string DiagnosisCode { get; set; } = string.Empty;
        public string DateDiagnosed { get; set; } = string.Empty;
        public int DiagnosisStatusId { get; set; }
        public string DiagnosisStatus { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string LongDescription { get; set; } = string.Empty;
    }
}
