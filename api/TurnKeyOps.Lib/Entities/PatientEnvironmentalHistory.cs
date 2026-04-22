using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientEnvironmentalHistory : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid PatientId { get; set; }
        public DateTime DateNoted { get; set; } = DateTime.UtcNow;

        public string Occupation { get; set; } = string.Empty;
        public string OccupationRisk { get; set; } = string.Empty;
        public int YearsInOccupation { get; set; }

        public string ExposureRisk { get; set; } = string.Empty;
        public string ExposureDetails { get; set; } = string.Empty;
        public DateOnly? DateExposure { get; set; }

        public string RecentTravel { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public DateOnly? DateDeparture { get; set; }
        public int DaysAbroad { get; set; }

        public bool IsLocked { get; set; }
    }
}
