namespace MedInsights.Lib.Dtos
{
    public class TenantCreditBalanceDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public DateTime CurrentUsagePeriodStartUtc { get; set; }
        public DateTime CurrentUsagePeriodEndUtc { get; set; }
        public int PurchasedCreditsAvailable { get; set; }
        public DateTime PurchasedCreditsExpireAtUtc { get; set; }
        public bool SoftCapAlertEnabled { get; set; }
        public DateTime? LastTopUpUtc { get; set; }
        public int TopUpsThisCycle { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
