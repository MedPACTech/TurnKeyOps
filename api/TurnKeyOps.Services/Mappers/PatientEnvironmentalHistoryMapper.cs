using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientEnvironmentalHistoryMapper
    {
        public static PatientEnvironmentalHistoryDto ToDto(PatientEnvironmentalHistory entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientEnvironmentalHistoryDto
            {
                Id = id,
                PatientId = entity.PatientId,
                DateNoted = entity.DateNoted,
                Occupation = entity.Occupation,
                OccupationRisk = entity.OccupationRisk,
                YearsInOccupation = entity.YearsInOccupation,
                ExposureRisk = entity.ExposureRisk,
                ExposureDetails = entity.ExposureDetails,
                DateExposure = entity.DateExposure,
                RecentTravel = entity.RecentTravel,
                Destination = entity.Destination,
                DateDeparture = entity.DateDeparture,
                DaysAbroad = entity.DaysAbroad,
                IsLocked = entity.IsLocked
            };
        }

        public static PatientEnvironmentalHistory ToEntity(PatientEnvironmentalHistoryDto dto)
        {
            return new PatientEnvironmentalHistory
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                DateNoted = dto.DateNoted,
                Occupation = dto.Occupation,
                OccupationRisk = dto.OccupationRisk,
                YearsInOccupation = dto.YearsInOccupation,
                ExposureRisk = dto.ExposureRisk,
                ExposureDetails = dto.ExposureDetails,
                DateExposure = dto.DateExposure,
                RecentTravel = dto.RecentTravel,
                Destination = dto.Destination,
                DateDeparture = dto.DateDeparture,
                DaysAbroad = dto.DaysAbroad,
                IsLocked = dto.IsLocked,
                IsDeleted = false
            };
        }
    }
}
