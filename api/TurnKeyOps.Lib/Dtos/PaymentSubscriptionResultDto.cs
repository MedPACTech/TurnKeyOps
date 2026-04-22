namespace MedInsights.Lib.Dtos
{
    public sealed class PaymentSubscriptionResultDto
    {
        public string Provider { get; set; } = string.Empty;
        public string SubscriptionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int SeatCount { get; set; }
        public int? NextRenewalSeatCount { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public DateTime? CurrentPeriodStartUtc { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }
    }
}
