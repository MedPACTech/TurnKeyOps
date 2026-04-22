namespace MedInsights.Lib.Configurations
{
    public sealed class PayPalBillingCatalogSettings
    {
        public Dictionary<string, string> SubscriptionPlanMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, int> TopUpCreditAmountMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["credits-1000"] = 1000,
            ["credits-5000"] = 5000
        };

        public Dictionary<string, decimal> TopUpPriceAmountMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["credits-1000"] = 10m,
            ["credits-5000"] = 45m
        };
    }
}
