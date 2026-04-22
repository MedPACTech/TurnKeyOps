namespace MedInsights.Lib.Dtos
{
    public sealed class PaymentWebhookEventDto
    {
        public string Provider { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public string? CustomerId { get; set; }
        public string? SubscriptionId { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? RequestedByUserId { get; set; }
        public string? PriceKey { get; set; }
        public string? PurchaseType { get; set; }
        public string? Mode { get; set; }
        public string? Status { get; set; }
        public int? Quantity { get; set; }
        public int? SeatCount { get; set; }
        public DateTime? CurrentPeriodStartUtc { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }
        public bool? CancelAtPeriodEnd { get; set; }
    }
}
