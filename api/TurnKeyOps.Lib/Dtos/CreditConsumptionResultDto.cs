namespace MedInsights.Lib.Dtos
{
    public sealed class CreditConsumptionResultDto
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string UsagePeriodKey { get; set; } = string.Empty;
        public int RequestedCredits { get; set; }
        public int IncludedCreditsConsumed { get; set; }
        public int PurchasedCreditsConsumed { get; set; }
        public int IncludedCreditsRemaining { get; set; }
        public int PurchasedCreditsRemaining { get; set; }
        public DateTime EffectiveUtc { get; set; }
    }
}
