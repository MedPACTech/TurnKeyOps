using System;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientContextMapper
    {
        public static PatientContextDto ToDto(PatientContext entity)
        {
            var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
            return new PatientContextDto
            {
                Id = id,
                PatientId = Guid.Parse(entity.PatientId),
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                DateOfBirth = DateOnly.FromDateTime(entity.DateOfBirth),
                Gender = entity.Gender,
                DateActivated = DateTime.SpecifyKind(entity.DateActivated, DateTimeKind.Utc)
            };
        }

        public static PatientContext ToEntity(PatientContextDto dto)
        {
            return new PatientContext
            {
                Id = dto.Id,
                PatientId = dto.PatientId.ToString("D"),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                Gender = dto.Gender,
                DateActivated = DateTime.UtcNow,
                IsDeleted = false
            };
        }
    }
}
