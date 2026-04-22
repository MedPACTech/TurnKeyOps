using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class PricingRuleSnapshot : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public decimal SeatUnitPrice { get; set; }
        public int IncludedCreditsPerSeatPerMonth { get; set; }
        public decimal CadenceDiscountPercent { get; set; }
        public string? PromoType { get; set; }
        public decimal? PromoValue { get; set; }
        public DateTime? PromoStartUtc { get; set; }
        public DateTime? PromoEndUtc { get; set; }
        public DateTime? IntroOfferEndUtc { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
