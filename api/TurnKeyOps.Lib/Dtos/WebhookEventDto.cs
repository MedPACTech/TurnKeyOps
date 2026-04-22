namespace MedInsights.Lib.Dtos
{
    public class WebhookEventDto
    {
        public Guid Id { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string ProcessingStatus { get; set; } = string.Empty;
        public Guid? CorrelationTenantId { get; set; }
        public string? PayloadHash { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public DateTime? ProcessedUtc { get; set; }
    }
}
