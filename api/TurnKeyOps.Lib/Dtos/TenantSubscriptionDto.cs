namespace MedInsights.Lib.Dtos
{
    public class TenantSubscriptionDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string BillingCadence { get; set; } = string.Empty;
        public string SubscriptionStatus { get; set; } = string.Empty;
        public string? ProviderSubscriptionId { get; set; }
        public Guid? PricingRuleSnapshotId { get; set; }
        public int CurrentSeatCount { get; set; }
        public int NextRenewalSeatCount { get; set; }
        public bool CancelAtTermEnd { get; set; }
        public DateTime TermStartUtc { get; set; }
        public DateTime TermEndUtc { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
