using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

/// <summary>
/// Weather.gov API integration. FREE, no API key required.
/// Flow: /points/{lat},{lon} → gridpoint forecast URL → /gridpoints/{wfo}/{x},{y}/forecast
/// </summary>
public class WeatherService : IWeatherService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IJobSiteRepository _jobSiteRepo;
    private readonly WeatherSettings _settings;

    public WeatherService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IJobSiteRepository jobSiteRepo,
        IOptions<WeatherSettings> settings)
    {
        _http = httpClientFactory.CreateClient("WeatherGov");
        _cache = cache;
        _jobSiteRepo = jobSiteRepo;
        _settings = settings.Value;
    }

    public async Task<IEnumerable<WeatherForecastDto>> GetForecastAsync(double latitude, double longitude)
    {
        var cacheKey = $"weather_{latitude:F4}_{longitude:F4}";
        if (_cache.TryGetValue(cacheKey, out List<WeatherForecastDto>? cached) && cached is not null)
            return cached;

        // Step 1: Get the grid endpoint
        var pointsUrl = $"https://api.weather.gov/points/{latitude:F4},{longitude:F4}";
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);

        var pointsResponse = await _http.GetAsync(pointsUrl);
        if (!pointsResponse.IsSuccessStatusCode) return Enumerable.Empty<WeatherForecastDto>();

        var pointsJson = await pointsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var forecastUrl = pointsJson.GetProperty("properties").GetProperty("forecast").GetString();
        if (string.IsNullOrEmpty(forecastUrl)) return Enumerable.Empty<WeatherForecastDto>();

        // Step 2: Get the forecast
        var forecastResponse = await _http.GetAsync(forecastUrl);
        if (!forecastResponse.IsSuccessStatusCode) return Enumerable.Empty<WeatherForecastDto>();

        var forecastJson = await forecastResponse.Content.ReadFromJsonAsync<JsonElement>();
        var periods = forecastJson.GetProperty("properties").GetProperty("periods");

        var forecasts = new List<WeatherForecastDto>();
        foreach (var period in periods.EnumerateArray())
        {
            forecasts.Add(new WeatherForecastDto
            {
                Summary = period.GetProperty("shortForecast").GetString(),
                TempHigh = period.GetProperty("isDaytime").GetBoolean() ? period.GetProperty("temperature").GetInt32() : null,
                TempLow = !period.GetProperty("isDaytime").GetBoolean() ? period.GetProperty("temperature").GetInt32() : null,
                PrecipChance = period.TryGetProperty("probabilityOfPrecipitation", out var pop) &&
                               pop.TryGetProperty("value", out var popVal) && popVal.ValueKind == JsonValueKind.Number
                               ? popVal.GetInt32() : null,
                Icon = period.GetProperty("icon").GetString(),
                WindSpeed = null, // Could parse "10 mph" string if needed
                WindDirection = period.TryGetProperty("windDirection", out var wd) ? wd.GetString() : null,
                ForecastDate = period.TryGetProperty("startTime", out var st) ? DateTime.Parse(st.GetString()!) : null
            });
        }

        _cache.Set(cacheKey, forecasts, TimeSpan.FromMinutes(_settings.CacheMinutes));
        return forecasts;
    }

    public async Task<IEnumerable<WeatherForecastDto>> GetForecastForJobSiteAsync(Guid jobSiteId)
    {
        var jobSite = await _jobSiteRepo.GetByIdAsync(jobSiteId);
        if (jobSite is null || !jobSite.Latitude.HasValue || !jobSite.Longitude.HasValue)
            return Enumerable.Empty<WeatherForecastDto>();

        return await GetForecastAsync(jobSite.Latitude.Value, jobSite.Longitude.Value);
    }
}
