using System;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class PatientMapper
    {
        public static PatientDto ToDto(Patient entity)
        {
            var id = entity.Id != Guid.Empty ? entity.Id : Guid.Parse(entity.RowKey);
            return new PatientDto
            {
                Id = id,
                PatientId = id,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                DateOfBirth = DateOnly.FromDateTime(DateTime.SpecifyKind(entity.DateOfBirth, DateTimeKind.Utc)),
                Gender = entity.Gender,
                PatientStatus = string.IsNullOrWhiteSpace(entity.PatientStatus) ? "Active" : entity.PatientStatus,
                PhysicalAddressLine1 = entity.PhysicalAddressLine1,
                PhysicalAddressLine2 = entity.PhysicalAddressLine2,
                PhysicalCity = entity.PhysicalCity,
                PhysicalState = entity.PhysicalState,
                PhysicalPostalCode = entity.PhysicalPostalCode,
                PhysicalCountry = entity.PhysicalCountry,
                MailingAddressLine1 = entity.MailingAddressLine1,
                MailingAddressLine2 = entity.MailingAddressLine2,
                MailingCity = entity.MailingCity,
                MailingState = entity.MailingState,
                MailingPostalCode = entity.MailingPostalCode,
                MailingCountry = entity.MailingCountry,
                BillingAddressLine1 = entity.BillingAddressLine1,
                BillingAddressLine2 = entity.BillingAddressLine2,
                BillingCity = entity.BillingCity,
                BillingState = entity.BillingState,
                BillingPostalCode = entity.BillingPostalCode,
                BillingCountry = entity.BillingCountry,
                CurrentFacilityId = entity.CurrentFacilityId,
                CurrentFacilityName = entity.CurrentFacilityName,
                CurrentFacilityAdmitDate = entity.CurrentFacilityAdmitDate.HasValue
                    ? DateTime.SpecifyKind(entity.CurrentFacilityAdmitDate.Value, DateTimeKind.Utc)
                    : null,
                CurrentFacilityStatus = entity.CurrentFacilityStatus,
                DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
            };
        }

        public static Patient ToEntity(PatientDto dto, string partitionKey)
        {
            return new Patient
            {
                Id = dto.Id,
                PartitionKey = partitionKey,
                RowKey = RepositoryKeyHelper.ToRowKey(dto.Id),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                Gender = dto.Gender,
                PatientStatus = string.IsNullOrWhiteSpace(dto.PatientStatus) ? "Active" : dto.PatientStatus.Trim(),
                PhysicalAddressLine1 = Normalize(dto.PhysicalAddressLine1),
                PhysicalAddressLine2 = Normalize(dto.PhysicalAddressLine2),
                PhysicalCity = Normalize(dto.PhysicalCity),
                PhysicalState = Normalize(dto.PhysicalState),
                PhysicalPostalCode = Normalize(dto.PhysicalPostalCode),
                PhysicalCountry = Normalize(dto.PhysicalCountry),
                MailingAddressLine1 = Normalize(dto.MailingAddressLine1),
                MailingAddressLine2 = Normalize(dto.MailingAddressLine2),
                MailingCity = Normalize(dto.MailingCity),
                MailingState = Normalize(dto.MailingState),
                MailingPostalCode = Normalize(dto.MailingPostalCode),
                MailingCountry = Normalize(dto.MailingCountry),
                BillingAddressLine1 = Normalize(dto.BillingAddressLine1),
                BillingAddressLine2 = Normalize(dto.BillingAddressLine2),
                BillingCity = Normalize(dto.BillingCity),
                BillingState = Normalize(dto.BillingState),
                BillingPostalCode = Normalize(dto.BillingPostalCode),
                BillingCountry = Normalize(dto.BillingCountry),
                CurrentFacilityId = dto.CurrentFacilityId,
                CurrentFacilityName = dto.CurrentFacilityName,
                CurrentFacilityAdmitDate = dto.CurrentFacilityAdmitDate.HasValue
                    ? DateTime.SpecifyKind(dto.CurrentFacilityAdmitDate.Value, DateTimeKind.Utc)
                    : null,
                CurrentFacilityStatus = dto.CurrentFacilityStatus,
                DateCreated = dto.DateCreated.HasValue
                    ? DateTime.SpecifyKind(dto.DateCreated.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                DateUpdated = dto.DateUpdated.HasValue
                    ? DateTime.SpecifyKind(dto.DateUpdated.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                IsDeleted = false
            };
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
