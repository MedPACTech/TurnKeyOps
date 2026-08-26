using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[AllowAnonymous]
[Route("api/public/quote-requests")]
public sealed class PublicQuoteRequestsController : ApiControllerBase
{
    private readonly IQuoteRequestService _service;

    public PublicQuoteRequestsController(IQuoteRequestService service) => _service = service;

    [HttpPost("{tenantSlug}")]
    public async Task<IActionResult> Create(
        string tenantSlug,
        [FromBody] CreateQuoteRequestDto dto,
        CancellationToken ct)
    {
        var result = await _service.CreatePublicAsync(tenantSlug, dto, ct);
        return CreatedResponse(nameof(Create), new { tenantSlug }, result);
    }
}
