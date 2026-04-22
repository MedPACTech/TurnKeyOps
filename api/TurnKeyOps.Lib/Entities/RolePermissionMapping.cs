using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public sealed class RolePermissionMapping : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        [AzureTableProjectedColumn]
        public Guid? TenantId { get; set; }
        [AzureTableProjectedColumn]
        public Guid RoleId { get; set; }
        [AzureTableProjectedColumn]
        public string RoleKey { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public Guid? PermissionId { get; set; }
        [AzureTableProjectedColumn]
        public string PermissionKey { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public bool IsDeleted { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
