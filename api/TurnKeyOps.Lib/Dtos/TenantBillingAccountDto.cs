namespace MedInsights.Lib.Dtos
{
    public class TenantBillingAccountDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string BillingStatus { get; set; } = string.Empty;
        public string? ProviderCustomerId { get; set; }
        public string? DefaultPaymentMethodRef { get; set; }
        public bool AutoTopUpEnabled { get; set; }
        public string? TopUpPackSku { get; set; }
        public int TopUpTriggerThreshold { get; set; }
        public int MaxTopUpsPerCycle { get; set; }
        public decimal? MaxTopUpSpendPerCycle { get; set; }
        public DateTime? LastAutoTopUpAttemptUtc { get; set; }
        public DateTime? LastAutoTopUpSuccessUtc { get; set; }
        public DateTime? LastAutoTopUpFailureUtc { get; set; }
        public int AutoTopUpFailureCount { get; set; }
        public string? LastAutoTopUpError { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
