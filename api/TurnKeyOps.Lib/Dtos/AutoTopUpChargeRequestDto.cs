namespace MedInsights.Lib.Dtos
{
    public sealed class AutoTopUpChargeRequestDto
    {
        public string? Provider { get; set; }
        public Guid TenantId { get; set; }
        public Guid? RequestedByUserId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string PaymentMethodId { get; set; } = string.Empty;
        public string PriceKey { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
    }
}
