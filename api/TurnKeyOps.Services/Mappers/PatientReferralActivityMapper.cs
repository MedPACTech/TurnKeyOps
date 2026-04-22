using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientReferralActivityMapper
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static PatientReferralActivityDto ToDto(PatientReferralActivity entity)
        {
            return new PatientReferralActivityDto
            {
                Id = entity.Id,
                PatientReferralId = entity.PatientReferralId,
                ActivityType = entity.ActivityType,
                Title = entity.Title,
                Body = entity.Body,
                CreatedAt = entity.CreatedAtUtc.Kind == DateTimeKind.Utc
                    ? entity.CreatedAtUtc
                    : DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc),
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByName = entity.CreatedByName,
                Metadata = Deserialize<Dictionary<string, string?>>(entity.MetadataJson)
            };
        }

        public static PatientReferralActivity ToEntity(CreatePatientReferralActivityDto dto)
        {
            var createdAtUtc = dto.CreatedAtUtc ?? DateTime.UtcNow;

            return new PatientReferralActivity
            {
                Id = Guid.NewGuid(),
                PatientReferralId = dto.PatientReferralId,
                PatientId = dto.PatientId,
                ActivityType = dto.ActivityType.Trim(),
                Title = dto.Title.Trim(),
                Body = string.IsNullOrWhiteSpace(dto.Body) ? null : dto.Body.Trim(),
                CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
                CreatedByUserId = dto.CreatedByUserId,
                CreatedByName = string.IsNullOrWhiteSpace(dto.CreatedByName) ? null : dto.CreatedByName.Trim(),
                MetadataJson = Serialize(dto.Metadata),
                IsDeleted = false
            };
        }

        private static string? Serialize<T>(T? value)
            => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

        private static T? Deserialize<T>(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(value, JsonOptions);
            }
            catch (JsonException)
            {
                return default;
            }
        }
    }
}
