using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientFamilyMedicalHistoryMapper
    {
        public static PatientFamilyMedicalHistoryDto ToDto(PatientFamilyMedicalHistory entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientFamilyMedicalHistoryDto
            {
                Id = id,
                PatientId = entity.PatientId,
                DateNoted = entity.DateNoted,
                Description = entity.Description,
                Relationship = entity.Relationship
            };
        }

        public static PatientFamilyMedicalHistory ToEntity(PatientFamilyMedicalHistoryDto dto)
        {
            return new PatientFamilyMedicalHistory
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                DateNoted = dto.DateNoted,
                Description = dto.Description,
                Relationship = dto.Relationship,
                IsDeleted = false
            };
        }
    }
}
