using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class ActivityLogMapper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static ActivityLog ToEntity(ActivityLogUpsertDto dto)
        {
            var pk = RepositoryKeyHelper.BuildPartitionKey(dto.TenantId, dto.EntryDate);
            var rk = RepositoryKeyHelper.BuildRowKey(dto.EntryDate, dto.UserId);

            return new ActivityLog
            {
                PartitionKey = pk,
                RowKey = rk,

                EntryDate = dto.EntryDate.Date,
                TenantId = dto.TenantId,
                FacilityId = dto.FacilityId,
                UserId = dto.UserId,
                LogId = Guid.NewGuid(),

                ItemsJson = JsonSerializer.Serialize(dto.Items, JsonOptions),
                Narrative = dto.Narrative,

                EnteredBy = dto.EnteredBy,
                EnteredAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static ActivityLogDto ToDto(ActivityLog entity)
        {
            var items = JsonSerializer.Deserialize<List<ActivityLogItemDto>>(entity.ItemsJson, JsonOptions)
                        ?? new List<ActivityLogItemDto>();

            return new ActivityLogDto
            {
                EntryDate = entity.EntryDate,

                TenantId = entity.TenantId,
                FacilityId = entity.FacilityId,
                UserId = entity.UserId,
                LogId = entity.LogId,

                Items = items,
                Narrative = entity.Narrative,

                EnteredBy = entity.EnteredBy,
                EnteredAt = entity.EnteredAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
