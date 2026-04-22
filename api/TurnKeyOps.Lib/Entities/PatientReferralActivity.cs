using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public sealed class PatientReferralActivity : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public Guid Id { get; set; }

        [AzureTableProjectedColumn]
        public Guid PatientReferralId { get; set; }

        [AzureTableProjectedColumn]
        public Guid PatientId { get; set; }

        [AzureTableProjectedColumn]
        public string ActivityType { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }

        [AzureTableProjectedColumn]
        public DateTime CreatedAtUtc { get; set; }

        [AzureTableProjectedColumn]
        public Guid? CreatedByUserId { get; set; }

        public string? CreatedByName { get; set; }
        public string? MetadataJson { get; set; }

        [AzureTableProjectedColumn]
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
