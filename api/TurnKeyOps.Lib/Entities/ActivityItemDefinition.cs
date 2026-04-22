using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class ActivityItemDefinition : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid TenantId { get; set; }
        public string Key { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string ItemType { get; set; } = default!;
        public string? Unit { get; set; }

        public bool IsUserEntered { get; set; }
        public bool IsDerived { get; set; }
        public string? Formula { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
