using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class FacilityPatientAssignment : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid FacilityId { get; set; }
        public Guid PatientId { get; set; }

        [AzureTableProjectedColumn]
        public string PatientFirstName { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string PatientLastName { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public DateTime AdmitDate { get; set; }

        public DateTime? DischargeDate { get; set; }

        [AzureTableProjectedColumn]
        public string Status { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
