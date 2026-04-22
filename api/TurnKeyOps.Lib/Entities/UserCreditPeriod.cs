using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class UserCreditPeriod : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string PeriodKey { get; set; } = string.Empty;
        public int IncludedCreditsGranted { get; set; }
        public int IncludedCreditsConsumed { get; set; }
        public int PurchasedCreditsConsumed { get; set; }
        public int? SoftCapThreshold { get; set; }
        public DateTime? SoftCapAlertSentUtc { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
