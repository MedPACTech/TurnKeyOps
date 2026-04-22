namespace MedInsights.Lib.Dtos
{
    public sealed class CancelSubscriptionRequestDto
    {
        public string? Provider { get; set; }
        public string SubscriptionId { get; set; } = string.Empty;
    }
}
