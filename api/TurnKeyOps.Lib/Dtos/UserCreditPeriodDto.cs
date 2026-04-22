namespace MedInsights.Lib.Dtos
{
    public class UserCreditPeriodDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string PeriodKey { get; set; } = string.Empty;
        public int IncludedCreditsGranted { get; set; }
        public int IncludedCreditsConsumed { get; set; }
        public int PurchasedCreditsConsumed { get; set; }
        public int? SoftCapThreshold { get; set; }
        public DateTime? SoftCapAlertSentUtc { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
