using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public sealed class PatientClinicalSummaryCache : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public Guid Id { get; set; }

        [AzureTableProjectedColumn]
        public Guid TenantId { get; set; }

        [AzureTableProjectedColumn]
        public Guid PatientId { get; set; }

        [AzureTableProjectedColumn]
        public DateTime GeneratedAtUtc { get; set; }

        [AzureTableProjectedColumn]
        public string DataFingerprint { get; set; } = string.Empty;

        public string Narrative { get; set; } = string.Empty;
        public string ActiveConditionsJson { get; set; } = "[]";
        public string MostRecentConcern { get; set; } = string.Empty;
        public string CareGapsJson { get; set; } = "[]";
        public string NextVisit { get; set; } = string.Empty;
        public string ReferralCaseSummary { get; set; } = string.Empty;
        public string ReferralReason { get; set; } = string.Empty;
        public string Limitations { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
