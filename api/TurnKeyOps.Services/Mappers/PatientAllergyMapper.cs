using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientAllergyMapper
    {
        public static PatientAllergyDto ToDto(PatientAllergy entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientAllergyDto
            {
                Id = id,
                PatientId = entity.PatientId,
                AllergyType = entity.AllergyType,
                Severity = entity.Severity,
                Description = entity.Description,
                Reaction = entity.Reaction,
                DateNoted = entity.DateNoted
            };
        }

        public static PatientAllergy ToEntity(PatientAllergyDto dto)
        {
            return new PatientAllergy
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                AllergyType = dto.AllergyType,
                Severity = dto.Severity,
                Description = dto.Description,
                Reaction = dto.Reaction,
                DateNoted = dto.DateNoted,
                IsDeleted = false
            };
        }
    }
}
