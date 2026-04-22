using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Entities
{
    public sealed class AppointmentTypeDefinition : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public AppointmentTypeLocation Location { get; set; } = AppointmentTypeLocation.Facility;
        public bool IsActive { get; set; } = true;
        public int AverageTimeInMinutes { get; set; } = 30;
        public string? Data { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
