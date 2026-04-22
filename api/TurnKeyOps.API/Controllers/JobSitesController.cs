using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
public class JobSitesController : ApiControllerBase
{
    private readonly IJobSiteService _service;

    public JobSitesController(IJobSiteService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _service.GetAsync(id);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] int pageSize = 20, [FromQuery] string? continuationToken = null)
    {
        var (items, token) = await _service.GetPagedAsync(pageSize, continuationToken);
        return OkPagedResponse(items, pageSize, token);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JobSiteDto dto)
    {
        var result = await _service.AddAsync(dto);
        return CreatedResponse(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] JobSiteDto dto)
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
