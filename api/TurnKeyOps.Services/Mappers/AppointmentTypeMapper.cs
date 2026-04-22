using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class AppointmentTypeMapper
    {
        public static AppointmentTypeDto ToDto(AppointmentTypeDefinition entity)
        {
            return new AppointmentTypeDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Location = entity.Location,
                IsActive = entity.IsActive,
                AverageTimeInMinutes = entity.AverageTimeInMinutes,
                Data = entity.Data,
                DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                DateUpdated = entity.DateUpdated.HasValue
                    ? DateTime.SpecifyKind(entity.DateUpdated.Value, DateTimeKind.Utc)
                    : null
            };
        }
    }
}
