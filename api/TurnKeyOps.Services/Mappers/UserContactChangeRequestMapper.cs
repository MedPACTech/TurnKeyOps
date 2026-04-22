using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class UserContactChangeRequestMapper
    {
        public static UserContactChangeRequestDto ToDto(UserContactChangeRequest entity) => new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            TenantId = entity.TenantId,
            Channel = entity.Channel,
            NewContactValueMasked = Mask(entity.Channel, entity.NewContactValue),
            Status = entity.Status,
            RequestedUtc = entity.RequestedUtc,
            ExpiresUtc = entity.ExpiresUtc,
            VerifiedUtc = entity.VerifiedUtc
        };

        private static string Mask(string channel, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (string.Equals(channel, "email", StringComparison.OrdinalIgnoreCase))
            {
                var at = value.IndexOf('@');
                if (at <= 1)
                    return "***" + (at >= 0 ? value[at..] : string.Empty);

                return value[0] + "***" + value[(at - 1)..];
            }

            if (value.Length <= 4)
                return "****";

            return new string('*', Math.Max(0, value.Length - 4)) + value[^4..];
        }
    }
}
