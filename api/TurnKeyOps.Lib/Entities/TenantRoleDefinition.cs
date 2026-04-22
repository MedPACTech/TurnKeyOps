using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public sealed class TenantRoleDefinition : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        [AzureTableProjectedColumn]
        public Guid? TenantId { get; set; }
        [AzureTableProjectedColumn]
        public string Key { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public bool IsSystem { get; set; }
        [AzureTableProjectedColumn]
        public bool IsAssignable { get; set; }
        [AzureTableProjectedColumn]
        public bool GrantsOwnership { get; set; }
        [AzureTableProjectedColumn]
        public bool GrantsBillingAdmin { get; set; }
        [AzureTableProjectedColumn]
        public bool IsDeleted { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
