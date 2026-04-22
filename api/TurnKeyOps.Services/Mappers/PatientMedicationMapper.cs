using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientMedicationMapper
    {
        public static PatientMedicationDto ToDto(PatientMedication entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientMedicationDto
            {
                Id = id,
                PatientId = entity.PatientId,
                ProviderId = entity.ProviderId,
                PrescriberId = entity.PrescriberId,
                PrescriberName = entity.PrescriberName,
                DateNoted = entity.DateNoted,
                MedicationId = entity.MedicationId,
                Medication = entity.Medication,
                Strength = entity.Strength,
                Route = entity.Route,
                Frequency = entity.Frequency,
                IsEnded = entity.IsEnded,
                DateEnded = entity.DateEnded,
                IsLocked = entity.IsLocked
            };
        }

        public static PatientMedication ToEntity(PatientMedicationDto dto)
        {
            return new PatientMedication
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                ProviderId = dto.ProviderId,
                PrescriberId = dto.PrescriberId,
                PrescriberName = dto.PrescriberName,
                DateNoted = dto.DateNoted,
                MedicationId = dto.MedicationId,
                Medication = dto.Medication,
                Strength = dto.Strength,
                Route = dto.Route,
                Frequency = dto.Frequency,
                IsEnded = dto.IsEnded,
                DateEnded = dto.DateEnded,
                IsLocked = dto.IsLocked,
                IsDeleted = false
            };
        }
    }
}
