using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public sealed class ReferralWorkItem : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public Guid Id { get; set; }

        [AzureTableProjectedColumn]
        public Guid TenantId { get; set; }

        [AzureTableProjectedColumn]
        public Guid? PatientId { get; set; }

        [AzureTableProjectedColumn]
        public Guid? EncounterId { get; set; }

        [AzureTableProjectedColumn]
        public string Status { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string Signal { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string Assignee { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public DateTime DateCreated { get; set; }

        [AzureTableProjectedColumn]
        public DateTime DateUpdated { get; set; }

        [AzureTableProjectedColumn]
        public bool IsDeleted { get; set; }

        public string PatientName { get; set; } = string.Empty;
        public string Mrn { get; set; } = string.Empty;
        public string ReferralSource { get; set; } = string.Empty;
        public string ReferralChannel { get; set; } = string.Empty;
        public string SourceReceivedAt { get; set; } = string.Empty;
        public string CaseTitle { get; set; } = string.Empty;
        public string CaseSummary { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string OwnerRole { get; set; } = string.Empty;
        public string NextAction { get; set; } = string.Empty;
        public string NextActionAt { get; set; } = string.Empty;
        public string LastUpdate { get; set; } = string.Empty;
        public string LastUpdateNote { get; set; } = string.Empty;
        public string ReasonInQueue { get; set; } = string.Empty;
        public string QueueLane { get; set; } = string.Empty;
        public string BlockerLabel { get; set; } = string.Empty;
        public string DueLabel { get; set; } = string.Empty;
        public string DueClock { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string LatestNoteAuthor { get; set; } = string.Empty;
        public string PatientDetailsJson { get; set; } = "[]";
        public string TimelineJson { get; set; } = "[]";

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
