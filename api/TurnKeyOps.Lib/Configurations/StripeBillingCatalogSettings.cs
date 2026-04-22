namespace MedInsights.Lib.Configurations
{
    public sealed class StripeBillingCatalogSettings
    {
        public Dictionary<string, string> SubscriptionPriceMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["pro-monthly"] = "price_1Rxu8zDf25g0ISizZ4kp54UP",
            ["starter-annual"] = "price_456",
            ["team-monthly"] = "price_789"
        };

        public Dictionary<string, string> TopUpPriceMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["credits-1000"] = "price_topup_1000",
            ["credits-5000"] = "price_topup_5000"
        };

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
