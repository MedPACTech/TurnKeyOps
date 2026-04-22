using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class OperationalAlertMapper
    {
        public static OperationalAlertDto ToDto(OperationalAlert entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AlertType = entity.AlertType,
            Severity = entity.Severity,
            Status = entity.Status,
            DedupeKey = entity.DedupeKey,
            Source = entity.Source,
            Message = entity.Message,
            ContextJson = entity.ContextJson,
            RepeatCount = entity.RepeatCount,
            FirstOccurredUtc = entity.FirstOccurredUtc,
            LastOccurredUtc = entity.LastOccurredUtc,
            AcknowledgedUtc = entity.AcknowledgedUtc,
            AcknowledgedByUserId = entity.AcknowledgedByUserId,
            ResolvedUtc = entity.ResolvedUtc
        };
    }
}
