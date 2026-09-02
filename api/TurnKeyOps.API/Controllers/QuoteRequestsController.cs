using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/quote-requests")]
public sealed class QuoteRequestsController : ApiControllerBase
{
    private readonly IQuoteRequestService _service;

    public QuoteRequestsController(IQuoteRequestService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        OkResponse(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _service.GetAsync(id, ct);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] QuoteRequestDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, dto, ct);
        return result is null ? NotFound() : OkResponse(result);
    }
}
