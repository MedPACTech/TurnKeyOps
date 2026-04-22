using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Dtos
{
    public sealed class AppointmentTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public AppointmentTypeLocation Location { get; set; } = AppointmentTypeLocation.Facility;
        public bool IsActive { get; set; } = true;
        public int AverageTimeInMinutes { get; set; } = 30;
        public string? Data { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
