using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
public class CustomersController : ApiControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _service.GetAsync(id);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] int pageSize = 20, [FromQuery] string? continuationToken = null)
    {
        if (pageSize <= 0) return BadRequestResponse("PageSize must be greater than zero.", nameof(pageSize));
        var (items, token) = await _service.GetPagedAsync(pageSize, continuationToken);
        return OkPagedResponse(items, pageSize, token);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query)
    {
        var results = await _service.SearchAsync(query);
        return OkResponse(results);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDto dto)
    {
        var result = await _service.AddAsync(dto);
        return CreatedResponse(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] CustomerDto dto)
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
