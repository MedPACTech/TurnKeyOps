using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class FacilityMapper
    {
        public static FacilityDto ToDto(Facility entity)
        {
            return new FacilityDto
            {
                Id = entity.Id,
                FacilityName = entity.FacilityName,
                LogoUrl = entity.LogoUrl,
                Website = entity.Website,
                AddressLine1 = entity.AddressLine1,
                AddressLine2 = entity.AddressLine2,
                City = entity.City,
                State = entity.State,
                PostalCode = entity.PostalCode,
                IsResidential = entity.IsResidential,
                NumberOfBeds = entity.NumberOfBeds,
                PointOfContactName = entity.PointOfContactName,
                PointOfContactEmail = entity.PointOfContactEmail,
                PointOfContactPhone = entity.PointOfContactPhone,
                DateCreated = entity.DateCreated,
                DateUpdated = entity.DateUpdated
            };
        }

        public static Facility ToEntity(FacilityDto dto, string partitionKey)
        {
            return new Facility
            {
                Id = dto.Id,
                PartitionKey = partitionKey,
                RowKey = dto.Id == Guid.Empty ? string.Empty : EntityKeyPolicy.Row(dto.Id),
                FacilityName = dto.FacilityName.Trim(),
                LogoUrl = Normalize(dto.LogoUrl),
                Website = Normalize(dto.Website),
                AddressLine1 = Normalize(dto.AddressLine1),
                AddressLine2 = Normalize(dto.AddressLine2),
                City = Normalize(dto.City),
                State = Normalize(dto.State),
                PostalCode = Normalize(dto.PostalCode),
                IsResidential = dto.IsResidential,
                NumberOfBeds = dto.NumberOfBeds,
                PointOfContactName = Normalize(dto.PointOfContactName),
                PointOfContactEmail = Normalize(dto.PointOfContactEmail),
                PointOfContactPhone = Normalize(dto.PointOfContactPhone),
                DateCreated = dto.DateCreated ?? DateTime.UtcNow,
                DateUpdated = dto.DateUpdated
            };
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
