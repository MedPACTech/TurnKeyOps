using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class TenantCreditBalance : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }
        public DateTime CurrentUsagePeriodStartUtc { get; set; }
        public DateTime CurrentUsagePeriodEndUtc { get; set; }
        public int PurchasedCreditsAvailable { get; set; }
        public DateTime PurchasedCreditsExpireAtUtc { get; set; }
        public bool SoftCapAlertEnabled { get; set; }
        public DateTime? LastTopUpUtc { get; set; }
        public int TopUpsThisCycle { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
