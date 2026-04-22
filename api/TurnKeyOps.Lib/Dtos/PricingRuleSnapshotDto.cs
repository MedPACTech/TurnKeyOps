namespace MedInsights.Lib.Dtos
{
    public class PricingRuleSnapshotDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public decimal SeatUnitPrice { get; set; }
        public int IncludedCreditsPerSeatPerMonth { get; set; }
        public decimal CadenceDiscountPercent { get; set; }
        public string? PromoType { get; set; }
        public decimal? PromoValue { get; set; }
        public DateTime? PromoStartUtc { get; set; }
        public DateTime? PromoEndUtc { get; set; }
        public DateTime? IntroOfferEndUtc { get; set; }
        public DateTime? DateCreated { get; set; }
    }
}
