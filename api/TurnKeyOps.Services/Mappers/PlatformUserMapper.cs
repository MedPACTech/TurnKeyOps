using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PlatformUserMapper
    {
        public static PlatformUserDto ToDto(PlatformUser entity) => new()
        {
            Id = entity.Id,
            PrimaryEmail = entity.PrimaryEmail,
            PrimaryPhone = entity.PrimaryPhone,
            EmailVerified = entity.EmailVerified,
            PhoneVerified = entity.PhoneVerified,
            IsActive = entity.IsActive,
            DateCreated = entity.DateCreated,
            DateUpdated = entity.DateUpdated
        };

        public static PlatformUser ToEntity(PlatformUserDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            PrimaryEmail = Normalize(dto.PrimaryEmail),
            PrimaryPhone = Normalize(dto.PrimaryPhone),
            NormalizedPrimaryEmail = Normalize(dto.PrimaryEmail)?.ToUpperInvariant(),
            NormalizedPrimaryPhone = NormalizePhone(dto.PrimaryPhone),
            EmailVerified = dto.EmailVerified,
            PhoneVerified = dto.PhoneVerified,
            IsActive = dto.IsActive,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string? NormalizePhone(string? value) => string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsDigit).ToArray());
    }
}
