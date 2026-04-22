namespace MedInsights.Lib.Dtos
{
    public sealed class ScheduleSeatReductionRequestDto
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public int SeatCount { get; set; }
    }
}
