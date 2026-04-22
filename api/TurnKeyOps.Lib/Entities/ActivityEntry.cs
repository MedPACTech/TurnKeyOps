using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class ActivityItems : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid TenantId { get; set; }
        public Guid? FacilityId { get; set; }
        public Guid UserId { get; set; }
        public Guid LogId { get; set; }

        public string UserFirstName { get; set; } = default!;
        public string UserLastName { get; set; } = default!;

        public string ItemKey { get; set; } = default!;
        public string ItemType { get; set; } = default!;
        public double NumericValue { get; set; }
        public string? Unit { get; set; }
        public Guid EnteredBy { get; set; }
        public DateTime EnteredAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime EntryDate { get; set; }

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
