using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class UserProfileMapper
    {
        public static UserProfileDto ToDto(UserProfile entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            Guid? tenantId = null;
            if (!string.IsNullOrWhiteSpace(entity.PartitionKey) && Guid.TryParse(entity.PartitionKey, out var parsedTenantId))
                tenantId = parsedTenantId;

            return new UserProfileDto
            {
                Id = id,
                TenantId = tenantId,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                PrimaryPhone = entity.PrimaryPhone,
                PrimaryEmail = entity.PrimaryEmail,
                SecondaryPhone = entity.SecondaryPhone,
                SecondaryEmail = entity.SecondaryEmail,
                AddressLine1 = entity.AddressLine1,
                AddressLine2 = entity.AddressLine2,
                City = entity.City,
                State = entity.State,
                PostalCode = entity.PostalCode,
                Title = entity.Title,
                Suffix = entity.Suffix,
                IsActive = entity.IsActive
            };
        }

        public static UserProfile ToEntity(UserProfileDto dto, string partitionKey, string rowKey)
        {
            return new UserProfile
            {
                Id = dto.Id,
                PartitionKey = partitionKey,
                RowKey = rowKey,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PrimaryPhone = dto.PrimaryPhone,
                PrimaryEmail = dto.PrimaryEmail,
                SecondaryPhone = dto.SecondaryPhone,
                SecondaryEmail = dto.SecondaryEmail,
                AddressLine1 = dto.AddressLine1,
                AddressLine2 = dto.AddressLine2,
                City = dto.City,
                State = dto.State,
                PostalCode = dto.PostalCode,
                Title = dto.Title,
                Suffix = dto.Suffix,
                IsActive = dto.IsActive,
                IsDeleted = false
            };
        }
    }
}
