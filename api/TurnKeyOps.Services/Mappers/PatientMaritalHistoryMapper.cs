using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientMaritalHistoryMapper
    {
        public static PatientMaritalHistoryDto ToDto(PatientMaritalHistory entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientMaritalHistoryDto
            {
                Id = id,
                PatientId = entity.PatientId,
                DateNoted = entity.DateNoted,
                MaritalStatus = entity.MaritalStatus,
                DateMarried = entity.DateMarried,
                DateDivorced = entity.DateDivorced,
                DateWidowed = entity.DateWidowed,
                SpouseName = entity.SpouseName,
                HasChildren = entity.HasChildren,
                NumberOfChildren = entity.NumberOfChildren,
                Notes = entity.Notes
            };
        }

        public static PatientMaritalHistory ToEntity(PatientMaritalHistoryDto dto)
        {
            return new PatientMaritalHistory
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                DateNoted = dto.DateNoted,
                MaritalStatus = dto.MaritalStatus,
                DateMarried = dto.DateMarried,
                DateDivorced = dto.DateDivorced,
                DateWidowed = dto.DateWidowed,
                SpouseName = dto.SpouseName ?? string.Empty,
                HasChildren = dto.HasChildren ?? string.Empty,
                NumberOfChildren = dto.NumberOfChildren,
                Notes = dto.Notes ?? string.Empty,
                IsDeleted = false
            };
        }
    }
}
