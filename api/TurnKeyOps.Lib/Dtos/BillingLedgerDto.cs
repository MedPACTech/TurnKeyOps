namespace MedInsights.Lib.Dtos
{
    public class BillingLedgerDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string? ProviderEventId { get; set; }
        public string? ProviderInvoiceId { get; set; }
        public string? ProviderPaymentIntentId { get; set; }
        public string? ProviderSubscriptionId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? Description { get; set; }
        public DateTime EffectiveUtc { get; set; }
    }
}
