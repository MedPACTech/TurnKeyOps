using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Dtos
{
    public class PatientVitalsDto
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? PatientEncounterId { get; set; }
        public VitalsUnitSystem UnitSystem { get; set; } = VitalsUnitSystem.Imperial;
        public decimal? Temperature { get; set; }
        public decimal? Tmax { get; set; }
        public int? SystolicBloodPressure { get; set; }
        public int? DiastolicBloodPressure { get; set; }
        public int? RespitoryRate { get; set; }
        public int? HeartRate { get; set; }
        public string? HeartRateQuality { get; set; }
        public string? PulseOximetry { get; set; }
        public decimal? Height { get; set; }
        public decimal? Weight { get; set; }
        public DateTime DateRead { get; set; } = DateTime.UtcNow;
        public decimal? BMI { get; set; }
    }
}
