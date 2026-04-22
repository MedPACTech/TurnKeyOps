namespace MedInsights.Lib.Dtos
{
    public sealed class ReactivateSubscriptionRequestDto
    {
        public string? Provider { get; set; }
        public string SubscriptionId { get; set; } = string.Empty;
    }
}
