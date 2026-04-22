namespace TurnKeyOps.Lib.Configurations;

public class WeatherSettings
{
    /// <summary>User-Agent required by Weather.gov API.</summary>
    public string UserAgent { get; set; } = "TurnKeyOps/1.0 (contact@turnkeyops.ai)";

    /// <summary>Cache duration in minutes for weather forecasts.</summary>
    public int CacheMinutes { get; set; } = 30;
}
