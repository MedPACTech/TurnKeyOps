namespace MedInsights.Lib.Dtos
{
    public sealed class CreditUsageMessageDto
    {
        public Guid RequestId { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string UsagePeriodKey { get; set; } = string.Empty;
        public int Credits { get; set; }
        public string? SourceReference { get; set; }
        public string? Description { get; set; }
        public DateTime? EffectiveUtc { get; set; }
    }
}
