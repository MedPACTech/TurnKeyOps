namespace MedInsights.Lib.Dtos
{
    public sealed class PurchaseCreditTopUpRequestDto
    {
        public string? Provider { get; set; }
        public string PriceKey { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public Guid? TenantId { get; set; }
        public Guid? RequestedByUserId { get; set; }
    }
}
