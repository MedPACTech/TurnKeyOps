using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientOrderMapper
    {
        public static PatientOrderDto ToDto(PatientOrder entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientOrderDto
            {
                Id = id,
                PatientId = entity.PatientId,
                DateOrdered = entity.DateOrdered,
                OrderingProviderId = entity.OrderingProviderId,
                OrderingProviderName = entity.OrderingProviderName,
                LabProvider = entity.LabProvider,
                LabType = entity.LabType,
                LabId = entity.LabId,
                IsComplete = entity.IsComplete
            };
        }

        public static PatientOrder ToEntity(PatientOrderDto dto)
        {
            return new PatientOrder
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                DateOrdered = dto.DateOrdered,
                OrderingProviderId = dto.OrderingProviderId,
                OrderingProviderName = dto.OrderingProviderName,
                LabProvider = dto.LabProvider,
                LabType = dto.LabType,
                LabId = dto.LabId,
                IsComplete = dto.IsComplete,
                IsDeleted = false
            };
        }
    }
}
