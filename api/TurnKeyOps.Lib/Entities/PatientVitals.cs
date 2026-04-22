using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class PatientVitals : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? PatientEncounterId { get; set; }
        public decimal? TemperatureCelsius { get; set; }
        public decimal? TemperatureFahrenheit { get; set; }
        public decimal? TmaxCelsius { get; set; }
        public decimal? TmaxFahrenheit { get; set; }
        public int? SystolicBloodPressure { get; set; }
        public int? DiastolicBloodPressure { get; set; }
        public int? RespitoryRate { get; set; }
        public int? HeartRate { get; set; }
        public string? HeartRateQuality { get; set; }
        public string? PulseOximetry { get; set; }
        public decimal? HeightCentimeters { get; set; }
        public decimal? HeightInches { get; set; }
        public decimal? WeightKilograms { get; set; }
        public decimal? WeightPounds { get; set; }
        public DateTime DateRead { get; set; } = DateTime.UtcNow;
        public decimal? BMI { get; set; }
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
