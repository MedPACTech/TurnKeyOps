using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class PatientReferral : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        public Guid Id { get; set; }
        [AzureTableProjectedColumn]
        public Guid? EncounterId { get; set; }
        [AzureTableProjectedColumn]
        public Guid CaptureDraftNoteId { get; set; }
        [AzureTableProjectedColumn]
        public Guid PatientId { get; set; }
        [AzureTableProjectedColumn]
        public Guid ProviderId { get; set; }
        public string NoteType { get; set; } = string.Empty;
        public string NoteTitle { get; set; } = string.Empty;
        public string ReferralBody { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public string Status { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public string AssignedToName { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public string OwnerRole { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public string NextAction { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public DateTime? NextActionAt { get; set; }
        [AzureTableProjectedColumn]
        public DateTime? DueAt { get; set; }
        [AzureTableProjectedColumn]
        public string Priority { get; set; } = string.Empty;
        public string ReferralSource { get; set; } = string.Empty;
        public string SourceApp { get; set; } = string.Empty;
        public string ReferralChannel { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string CaseTitle { get; set; } = string.Empty;
        public string CaseSummary { get; set; } = string.Empty;
        public string ReferralReason { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public Guid? CreatedByUserId { get; set; }
        public string CreatedByFirstName { get; set; } = string.Empty;
        public string CreatedByLastName { get; set; } = string.Empty;
        public DateTime DateSent { get; set; }
        public Guid SentBy { get; set; }
        public string SentTo { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        [AzureTableProjectedColumn]
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
