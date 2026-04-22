using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MedInsights.Lib.Utils;

namespace MedInsights.Lib.Dtos
{
    public class PatientEnvironmentalHistoryDto
    {
        public Guid Id { get; set; }
        [Required] public Guid PatientId { get; set; }
        public DateTime DateNoted { get; set; }

        public string Occupation { get; set; } = string.Empty;
        [Required] public string OccupationRisk { get; set; } = string.Empty;
        public int YearsInOccupation { get; set; }

        [Required] public string ExposureRisk { get; set; } = string.Empty;
        public string ExposureDetails { get; set; } = string.Empty;
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly? DateExposure { get; set; }

        [Required] public string RecentTravel { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly? DateDeparture { get; set; }
        public int DaysAbroad { get; set; }

        public bool IsLocked { get; set; }
    }
}
