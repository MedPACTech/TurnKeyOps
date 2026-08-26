using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
public class WeatherController : ApiControllerBase
{
    private readonly IWeatherService _service;

    public WeatherController(IWeatherService service) => _service = service;

    /// <summary>Get 7-day forecast by lat/lon.</summary>
    [HttpGet]
    public async Task<IActionResult> GetForecast([FromQuery] double lat, [FromQuery] double lon)
    {
        var result = await _service.GetForecastAsync(lat, lon);
        return OkResponse(result);
    }

    /// <summary>Get forecast for a specific job site.</summary>
    [HttpGet("jobsite/{jobSiteId:guid}")]
    public async Task<IActionResult> GetForJobSite(Guid jobSiteId)
    {
        var result = await _service.GetForecastForJobSiteAsync(jobSiteId);
        return OkResponse(result);
    }
}
