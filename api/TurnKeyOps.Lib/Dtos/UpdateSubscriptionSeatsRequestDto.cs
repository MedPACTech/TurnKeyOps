namespace MedInsights.Lib.Dtos
{
    public sealed class UpdateSubscriptionSeatsRequestDto
    {
        public string? Provider { get; set; }
        public string SubscriptionId { get; set; } = string.Empty;
        public string? PriceKey { get; set; }
        public int SeatCount { get; set; }
        public string ProrationBehavior { get; set; } = "create_prorations";
    }
}
