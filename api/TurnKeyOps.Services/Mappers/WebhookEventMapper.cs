using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class WebhookEventMapper
    {
        public static WebhookEventDto ToDto(WebhookEvent entity) => new()
        {
            Id = entity.Id,
            Provider = entity.Provider,
            EventType = entity.EventType,
            ProcessingStatus = entity.ProcessingStatus,
            CorrelationTenantId = entity.CorrelationTenantId,
            PayloadHash = entity.PayloadHash,
            ReceivedUtc = entity.ReceivedUtc,
            ProcessedUtc = entity.ProcessedUtc
        };

        public static WebhookEvent ToEntity(WebhookEventDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            Provider = dto.Provider.Trim(),
            EventType = dto.EventType.Trim(),
            ProcessingStatus = dto.ProcessingStatus.Trim(),
            CorrelationTenantId = dto.CorrelationTenantId,
            PayloadHash = Normalize(dto.PayloadHash),
            ReceivedUtc = dto.ReceivedUtc,
            ProcessedUtc = dto.ProcessedUtc,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
