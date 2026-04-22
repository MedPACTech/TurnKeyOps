namespace MedInsights.Lib.Dtos
{
    public sealed class PaymentCheckoutSessionDto
    {
        public string Provider { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
