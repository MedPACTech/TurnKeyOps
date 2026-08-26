using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize]
[Route("api/quote-estimates")]
public sealed class QuoteEstimatesController : ApiControllerBase
{
    private readonly IQuoteEstimateService _service;
    public QuoteEstimatesController(IQuoteEstimateService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => OkResponse(await _service.ListAsync(ct));

    [HttpGet("{quoteRequestId:guid}")]
    public async Task<IActionResult> Get(Guid quoteRequestId, CancellationToken ct)
    {
        var result = await _service.GetAsync(quoteRequestId, ct);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpPut("{quoteRequestId:guid}")]
    public async Task<IActionResult> SaveDraft(
        Guid quoteRequestId,
        [FromBody] QuoteEstimateDraftInputDto input,
        CancellationToken ct) => OkResponse(await _service.SaveDraftAsync(quoteRequestId, input, ct));

    [HttpPost("{quoteRequestId:guid}/revisions")]
    public async Task<IActionResult> CreateRevision(
        Guid quoteRequestId,
        [FromBody] QuoteEstimateVersionRequest request,
        CancellationToken ct) => OkResponse(await _service.CreateRevisionAsync(quoteRequestId, request.ExpectedVersion, ct));

    [HttpPost("{quoteRequestId:guid}/send")]
    public async Task<IActionResult> Send(
        Guid quoteRequestId,
        [FromBody] QuoteEstimateVersionRequest request,
        CancellationToken ct) => OkResponse(await _service.SendAsync(
            quoteRequestId,
            request.ExpectedVersion,
            $"/bdr/estimate/{Uri.EscapeDataString(quoteRequestId.ToString())}",
            ct));
}

public sealed class QuoteEstimateVersionRequest
{
    public string? ExpectedVersion { get; set; }
}
