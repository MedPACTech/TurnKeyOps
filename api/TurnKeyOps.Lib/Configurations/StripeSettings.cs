

namespace MedInsights.Lib.Configurations
{
    public sealed class StripeSettings
    {
        public string SecretKey { get; set; } = "";
        public string PublicKey { get; set; } = "";
        public string WebhookSecret { get; set; } = "";
    }

}
