using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class PatientAppointmentMapper
    {
        public static PatientAppointmentDto ToDto(PatientAppointment entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;
            Guid? primaryContactId = null;
            if (!string.IsNullOrWhiteSpace(entity.PrimaryContactId) && Guid.TryParse(entity.PrimaryContactId, out var parsedPrimaryContactId))
                primaryContactId = parsedPrimaryContactId;

            return new PatientAppointmentDto
            {
                Id = id,
                PatientId = Guid.Parse(entity.PatientId),
                PatientFirstName = entity.PatientFirstName,
                PatientLastName = entity.PatientLastName,
                AppointmentTypeId = entity.AppointmentTypeId,
                AppointmentType = string.IsNullOrWhiteSpace(entity.AppointmentTypeName)
                    ? entity.AppointmentType.ToString()
                    : entity.AppointmentTypeName,
                AppointmentStartTime = DateTime.SpecifyKind(entity.AppointmentStartTime, DateTimeKind.Utc),
                AppointmentEndTime = DateTime.SpecifyKind(entity.AppointmentEndTime, DateTimeKind.Utc),
                PrimaryContactId = primaryContactId,
                PrimaryContactFirstName = entity.PrimaryContactFirstName,
                PrimaryContactLastName = entity.PrimaryContactLastName,
                PrimaryContactRelationship = entity.PrimaryContactRelationship,
                PrimaryContactPhone = entity.PrimaryContactPhone,
                PrimaryContactEmail = entity.PrimaryContactEmail,
                VisitAddressLine1 = entity.VisitAddressLine1,
                VisitAddressLine2 = entity.VisitAddressLine2,
                VisitCity = entity.VisitCity,
                VisitState = entity.VisitState,
                VisitPostalCode = entity.VisitPostalCode,
                VisitCountry = entity.VisitCountry,
                AppointmentStatus = entity.AppointmentStatus,
                AppointmentLocation = entity.AppointmentLocation,
                Reason = entity.Reason,
                DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                DateUpdated = entity.DateUpdated.HasValue
                    ? DateTime.SpecifyKind(entity.DateUpdated.Value, DateTimeKind.Utc)
                    : (DateTime?)null,
                UserId = Guid.Parse(entity.UserId),
                UserName = entity.DisplayName,
            };
        }

        public static PatientAppointment ToEntity(PatientAppointmentDto dto)
        {
            return new PatientAppointment
            {
                Id = dto.Id,
                RowKey = EntityKeyPolicy.Row(dto.Id),
                PatientId = EntityKeyPolicy.Row(dto.PatientId),
                PatientFirstName = dto.PatientFirstName,
                PatientLastName = dto.PatientLastName,
                AppointmentTypeId = dto.AppointmentTypeId,
                AppointmentTypeName = dto.AppointmentType,
                AppointmentType = MedInsights.Lib.Enums.AppointmentType.Other,
                AppointmentStartTime = DateTime.SpecifyKind(dto.AppointmentStartTime, DateTimeKind.Utc),
                AppointmentEndTime = DateTime.SpecifyKind(dto.AppointmentEndTime, DateTimeKind.Utc),
                PrimaryContactId = dto.PrimaryContactId.HasValue ? EntityKeyPolicy.Row(dto.PrimaryContactId.Value) : null,
                PrimaryContactFirstName = dto.PrimaryContactFirstName,
                PrimaryContactLastName = dto.PrimaryContactLastName,
                PrimaryContactRelationship = dto.PrimaryContactRelationship,
                PrimaryContactPhone = dto.PrimaryContactPhone,
                PrimaryContactEmail = dto.PrimaryContactEmail,
                VisitAddressLine1 = dto.VisitAddressLine1,
                VisitAddressLine2 = dto.VisitAddressLine2,
                VisitCity = dto.VisitCity,
                VisitState = dto.VisitState,
                VisitPostalCode = dto.VisitPostalCode,
                VisitCountry = dto.VisitCountry,
                AppointmentStatus = dto.AppointmentStatus,
                AppointmentLocation = dto.AppointmentLocation,
                Reason = dto.Reason,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                UserId = EntityKeyPolicy.Row(dto.UserId),
                DisplayName = dto.UserName,
                IsDeleted = false,
            };
        }
    }
}

