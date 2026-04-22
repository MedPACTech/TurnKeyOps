using System;

namespace MedInsights.Lib.Dtos
{
    /// <summary>
    /// Read model for an activity entry pulled from Azure Table Storage.
    /// Represents a single (user, date, type, key) metric.
    /// </summary>
    public class ActivityReadDto
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string UserFirstName { get; set; } = default!;
        public string UserLastName { get; set; } = default!;
        public DateTime EntryDate { get; set; }
        public string Type { get; set; } = default!; // e.g. "encounters", "admin_tasks"
        public string Key { get; set; } = default!;  // e.g. "training_room", "clinical"
        public double Value { get; set; }
        public string? Unit { get; set; }
    }
}