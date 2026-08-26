using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[AllowAnonymous]
[Route("api/public/quote-estimates")]
public sealed class PublicQuoteEstimatesController : ApiControllerBase
{
    private readonly IQuoteEstimateService _service;
    public PublicQuoteEstimatesController(IQuoteEstimateService service) => _service = service;

    [HttpGet("{tenantSlug}/{quoteRequestId:guid}")]
    public async Task<IActionResult> Get(
        string tenantSlug,
        Guid quoteRequestId,
        [FromQuery] string token,
        CancellationToken ct)
    {
        var result = await _service.GetPublicAsync(tenantSlug, quoteRequestId, token, ct);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpPost("{tenantSlug}/{quoteRequestId:guid}/approve")]
    public async Task<IActionResult> Approve(
        string tenantSlug,
        Guid quoteRequestId,
        [FromBody] QuoteEstimateDecisionDto decision,
        CancellationToken ct)
    {
        var result = await _service.ApproveAsync(tenantSlug, quoteRequestId, decision, ct);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpPost("{tenantSlug}/{quoteRequestId:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(
        string tenantSlug,
        Guid quoteRequestId,
        [FromBody] QuoteEstimateDecisionDto decision,
        CancellationToken ct)
    {
        var result = await _service.RequestChangesAsync(tenantSlug, quoteRequestId, decision, ct);
        return result is null ? NotFound() : OkResponse(result);
    }
}
