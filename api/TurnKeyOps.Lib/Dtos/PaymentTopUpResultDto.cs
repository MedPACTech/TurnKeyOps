namespace MedInsights.Lib.Dtos
{
    public sealed class PaymentTopUpResultDto
    {
        public string Provider { get; set; } = string.Empty;
        public string PriceKey { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? PaymentIntentId { get; set; }
        public string? InvoiceId { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
