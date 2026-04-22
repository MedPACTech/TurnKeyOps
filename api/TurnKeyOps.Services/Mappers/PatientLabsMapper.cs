using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientLabsMapper
    {
        public static PatientLabsDto ToDto(PatientLabs entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientLabsDto
            {
                Id = id,
                PatientId = entity.PatientId,
                DocumentId = entity.DocumentId,
                LabType = entity.LabType,
                LabProvider = entity.LabProvider,
                DateLabCompleted = entity.DateLabCompleted,
                LabStatus = entity.LabStatus,
                DateUploaded = entity.DateUploaded
            };
        }

        public static PatientLabs ToEntity(PatientLabsDto dto)
        {
            return new PatientLabs
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                DocumentId = dto.DocumentId,
                LabType = dto.LabType,
                LabProvider = dto.LabProvider,
                DateLabCompleted = dto.DateLabCompleted,
                LabStatus = dto.LabStatus,
                DateUploaded = dto.DateUploaded,
                IsDeleted = false
            };
        }
    }
}
