using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class DiagnosisCodeMapper
    {
        public static DiagnosisCodeDto ToDto(DiagnosisCode entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new DiagnosisCodeDto
            {
                Id = id,
                Code = entity.Code,
                ShortDescription = entity.ShortDescription,
                LongDescription = entity.LongDescription
            };
        }
    }
}
