namespace TurnKeyOps.Lib.Dtos;

public class WeatherForecastDto
{
    public string? Summary { get; set; }
    public int? TempHigh { get; set; }
    public int? TempLow { get; set; }
    public int? PrecipChance { get; set; }
    public string? Icon { get; set; }
    public double? WindSpeed { get; set; }
    public string? WindDirection { get; set; }
    public DateTime? ForecastDate { get; set; }
}
