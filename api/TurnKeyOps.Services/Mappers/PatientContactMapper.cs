using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientContactMapper
    {
        public static PatientContactDto ToDto(PatientContact entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientContactDto
            {
                PatientId = entity.PatientId,
                Id = id,
                ContactType = entity.ContactType,
                Relationship = entity.Relationship,
                OtherRelationship = entity.OtherRelationship,
                IsPrimary = entity.IsPrimary,
                IsSecondary = entity.IsSecondary,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                MiddleName = entity.MiddleName,
                OrganizationName = entity.OrganizationName,
                PrimaryPhone = entity.PrimaryPhone,
                SecondaryPhone = entity.SecondaryPhone,
                Email = entity.Email,
                AddressLine1 = entity.AddressLine1,
                AddressLine2 = entity.AddressLine2,
                City = entity.City,
                State = entity.State,
                PostalCode = entity.PostalCode,
                Country = entity.Country,
                PreferredContactMethod = entity.PreferredContactMethod,
                Notes = entity.Notes,
                HasHIPAAPermission = entity.HasHIPAAPermission,
                HasBillingPermission = entity.HasBillingPermission,
                HasDurablePowerOfAttorney = entity.HasDurablePowerOfAttorney,
                HasMedicalPowerOfAttorney = entity.HasMedicalPowerOfAttorney,
                HasFinancialPowerOfAttorney = entity.HasFinancialPowerOfAttorney
            };
        }

        public static PatientContact ToEntity(PatientContactDto dto)
        {
            return new PatientContact
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                ContactType = dto.ContactType,
                Relationship = dto.Relationship,
                OtherRelationship = string.IsNullOrWhiteSpace(dto.OtherRelationship) ? null : dto.OtherRelationship.Trim(),
                IsPrimary = dto.IsPrimary,
                IsSecondary = dto.IsSecondary,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                MiddleName = dto.MiddleName,
                OrganizationName = dto.OrganizationName,
                PrimaryPhone = dto.PrimaryPhone,
                SecondaryPhone = dto.SecondaryPhone,
                Email = dto.Email,
                AddressLine1 = dto.AddressLine1,
                AddressLine2 = dto.AddressLine2,
                City = dto.City,
                State = dto.State,
                PostalCode = dto.PostalCode,
                Country = dto.Country,
                PreferredContactMethod = dto.PreferredContactMethod,
                Notes = dto.Notes,
                HasHIPAAPermission = dto.HasHIPAAPermission,
                HasBillingPermission = dto.HasBillingPermission,
                HasDurablePowerOfAttorney = dto.HasDurablePowerOfAttorney,
                HasMedicalPowerOfAttorney = dto.HasMedicalPowerOfAttorney,
                HasFinancialPowerOfAttorney = dto.HasFinancialPowerOfAttorney,
                IsDeleted = false
            };
        }
    }
}
