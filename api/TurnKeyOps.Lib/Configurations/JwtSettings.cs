
namespace MedInsights.Lib.Configurations
{

    public sealed class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        // optional: keep this aligned with Program.cs
        public int ClockSkewSeconds { get; set; } = 120;
    }
}
