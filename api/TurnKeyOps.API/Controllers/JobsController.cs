using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
public class JobsController : ApiControllerBase
{
    private readonly IJobService _service;

    public JobsController(IJobService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] int pageSize = 20, [FromQuery] string? continuationToken = null, CancellationToken ct = default)
    {
        var (items, token) = await _service.GetPagedAsync(pageSize, continuationToken, ct);
        return OkPagedResponse(items, pageSize, token);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await _service.GetActiveAsync(ct);
        return OkResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JobDto dto, CancellationToken ct)
    {
        var result = await _service.AddAsync(dto, ct);
        return CreatedResponse(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] JobDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(dto, ct);
        return OkResponse(result);
    }

    [HttpPut("{id:guid}/schedule")]
    public async Task<IActionResult> Schedule(Guid id, [FromBody] JobScheduleInputDto input, CancellationToken ct)
    {
        var result = await _service.ScheduleAsync(id, input, ct);
        return OkResponse(result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] JobStatusInputDto input, CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(id, input, ct);
        return OkResponse(result);
    }

    [HttpPut("{id:guid}/planning")]
    public async Task<IActionResult> UpdatePlanning(Guid id, [FromBody] JobPlanningInputDto input, CancellationToken ct)
    {
        var result = await _service.UpdatePlanningAsync(id, input, ct);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] JobNoteInputDto input, CancellationToken ct)
    {
        var result = await _service.AddNoteAsync(id, input, ct);
        return OkResponse(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContentResponse();
    }
}
