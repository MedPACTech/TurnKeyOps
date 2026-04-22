using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
public class InvoicesController : ApiControllerBase
{
    private readonly IInvoiceService _service;

    public InvoicesController(IInvoiceService service) => _service = service;

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
    public async Task<IActionResult> Create([FromBody] InvoiceDto dto)
    {
        var result = await _service.AddAsync(dto);
        return CreatedResponse(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] InvoiceDto dto)
    {
        var result = await _service.UpdateAsync(dto);
        return OkResponse(result);
    }

    /// <summary>Create invoice from an accepted estimate.</summary>
    [HttpPost("from-estimate/{estimateId:guid}")]
    public async Task<IActionResult> CreateFromEstimate(Guid estimateId)
    {
        var result = await _service.CreateFromEstimateAsync(estimateId);
        return OkResponse(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContentResponse();
    }
}
