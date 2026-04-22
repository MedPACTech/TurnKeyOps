using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class PatientVitalsMapper
    {
        public static PatientVitalsDto ToDto(
            PatientVitals entity,
            VitalsUnitSystem unitSystem = VitalsUnitSystem.Imperial)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
            {
                id = parsedId;
            }

            return new PatientVitalsDto
            {
                Id = id,
                PatientId = entity.PatientId,
                PatientEncounterId = entity.PatientEncounterId,
                UnitSystem = unitSystem,
                Temperature = ConvertTemperature(entity.TemperatureCelsius, entity.TemperatureFahrenheit, unitSystem),
                Tmax = ConvertTemperature(entity.TmaxCelsius, entity.TmaxFahrenheit, unitSystem),
                SystolicBloodPressure = entity.SystolicBloodPressure,
                DiastolicBloodPressure = entity.DiastolicBloodPressure,
                RespitoryRate = entity.RespitoryRate,
                HeartRate = entity.HeartRate,
                HeartRateQuality = entity.HeartRateQuality,
                PulseOximetry = entity.PulseOximetry,
                Height = ConvertHeight(entity.HeightCentimeters, entity.HeightInches, unitSystem),
                Weight = ConvertWeight(entity.WeightKilograms, entity.WeightPounds, unitSystem),
                DateRead = entity.DateRead,
                BMI = entity.BMI
            };
        }

        public static PatientVitals ToEntity(PatientVitalsDto dto)
        {
            return new PatientVitals
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                PatientEncounterId = dto.PatientEncounterId,
                SystolicBloodPressure = dto.SystolicBloodPressure,
                DiastolicBloodPressure = dto.DiastolicBloodPressure,
                RespitoryRate = dto.RespitoryRate,
                HeartRate = dto.HeartRate,
                HeartRateQuality = dto.HeartRateQuality,
                PulseOximetry = dto.PulseOximetry,
                DateRead = dto.DateRead,
                BMI = dto.BMI,
                IsDeleted = false
            };
        }

        private static decimal? ConvertTemperature(decimal? celsius, decimal? fahrenheit, VitalsUnitSystem unitSystem)
        {
            if (unitSystem == VitalsUnitSystem.Metric)
            {
                if (celsius.HasValue) return celsius.Value;
                return fahrenheit.HasValue ? VitalsConversionTools.FahrenheitToCelsius(fahrenheit.Value) : null;
            }

            if (fahrenheit.HasValue) return fahrenheit.Value;
            return celsius.HasValue ? VitalsConversionTools.CelsiusToFahrenheit(celsius.Value) : null;
        }

        private static decimal? ConvertHeight(decimal? centimeters, decimal? inches, VitalsUnitSystem unitSystem)
        {
            if (unitSystem == VitalsUnitSystem.Metric)
            {
                if (centimeters.HasValue) return centimeters.Value;
                return inches.HasValue ? VitalsConversionTools.InchesToCentimeters(inches.Value) : null;
            }

            if (inches.HasValue) return inches.Value;
            return centimeters.HasValue ? VitalsConversionTools.CentimetersToInches(centimeters.Value) : null;
        }

        private static decimal? ConvertWeight(decimal? kilograms, decimal? pounds, VitalsUnitSystem unitSystem)
        {
            if (unitSystem == VitalsUnitSystem.Metric)
            {
                if (kilograms.HasValue) return kilograms.Value;
                return pounds.HasValue ? VitalsConversionTools.PoundsToKilograms(pounds.Value) : null;
            }

            if (pounds.HasValue) return pounds.Value;
            return kilograms.HasValue ? VitalsConversionTools.KilogramsToPounds(kilograms.Value) : null;
        }
    }
}
