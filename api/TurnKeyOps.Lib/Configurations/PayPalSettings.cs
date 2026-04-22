namespace MedInsights.Lib.Configurations
{
    public sealed class PayPalSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
        public string WebhookId { get; set; } = string.Empty;
        public string CustomerPortalUrl { get; set; } = string.Empty;
    }
}
