using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
public class CalendarController : ApiControllerBase
{
    private readonly ICalendarEventService _service;

    public CalendarController(ICalendarEventService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _service.GetAsync(id);
        return result is null ? NotFound() : OkResponse(result);
    }

    /// <summary>Get events for a date range (with weather overlay).</summary>
    [HttpGet]
    public async Task<IActionResult> GetByRange([FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        var results = await _service.GetByDateRangeAsync(start, end);
        return OkResponse(results);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CalendarEventDto dto)
    {
        var result = await _service.AddAsync(dto);
        return CreatedResponse(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] CalendarEventDto dto)
    {
        var result = await _service.UpdateAsync(dto);
        return OkResponse(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContentResponse();
    }
}
