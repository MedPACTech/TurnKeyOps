using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class AuditEventMapper
    {
        public static AuditEventDto ToDto(AuditEvent entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            Category = entity.Category,
            Action = entity.Action,
            Severity = entity.Severity,
            TargetType = entity.TargetType,
            TargetId = entity.TargetId,
            Source = entity.Source,
            Description = entity.Description,
            MetadataJson = entity.MetadataJson,
            OccurredUtc = entity.OccurredUtc
        };
    }
}
