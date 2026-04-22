using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class PlatformUser : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        [AzureTableProjectedColumn]
        public string? PrimaryEmail { get; set; }

        [AzureTableProjectedColumn]
        public string? PrimaryPhone { get; set; }

        public string? NormalizedPrimaryEmail { get; set; }
        public string? NormalizedPrimaryPhone { get; set; }
        public bool EmailVerified { get; set; }
        public bool PhoneVerified { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
