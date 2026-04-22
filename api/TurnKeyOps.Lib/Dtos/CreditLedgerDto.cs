namespace MedInsights.Lib.Dtos
{
    public class CreditLedgerDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string LedgerType { get; set; } = string.Empty;
        public string? SourceBucket { get; set; }
        public int Amount { get; set; }
        public int BalanceAfter { get; set; }
        public string? UsagePeriodKey { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string? SourceReference { get; set; }
        public string? Description { get; set; }
        public DateTime EffectiveUtc { get; set; }
    }
}
