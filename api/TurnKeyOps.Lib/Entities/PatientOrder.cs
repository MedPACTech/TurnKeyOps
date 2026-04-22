using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientOrder : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public DateOnly DateOrdered { get; set; }
        public Guid OrderingProviderId { get; set; }
        public string OrderingProviderName { get; set; } = string.Empty;
        public string LabProvider { get; set; } = string.Empty;
        public string LabType { get; set; } = string.Empty;
        public Guid LabId { get; set; }
        public bool IsComplete { get; set; }
    }
}
