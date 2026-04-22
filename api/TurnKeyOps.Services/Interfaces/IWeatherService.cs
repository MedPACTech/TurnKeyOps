using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IWeatherService
{
    /// <summary>Get 7-day forecast for a lat/lon (Weather.gov).</summary>
    Task<IEnumerable<WeatherForecastDto>> GetForecastAsync(double latitude, double longitude);

    /// <summary>Get weather for a specific job site by ID.</summary>
    Task<IEnumerable<WeatherForecastDto>> GetForecastForJobSiteAsync(Guid jobSiteId);
}
