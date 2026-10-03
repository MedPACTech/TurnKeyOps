using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using MedInsights.Lib.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/quote-estimates")]
public sealed class QuoteEstimatesController : ApiControllerBase
{
    private readonly IQuoteEstimateService _service;
    private readonly IOptions<QuoteRequestTenantOptions> _tenants;
    private readonly IUserContext _userContext;
    public QuoteEstimatesController(IQuoteEstimateService service, IOptions<QuoteRequestTenantOptions> tenants, IUserContext userContext)
    { _service = service; _tenants = tenants; _userContext = userContext; }
    private string ReviewPath(Guid requestId)
    {
        var slug = _tenants.Value.Tenants.FirstOrDefault(entry => entry.Value.TenantId == _userContext.TenantId).Key;
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("The current tenant has no configured quote review surface.");
        return $"/{Uri.EscapeDataString(slug)}/estimate/{requestId:D}";
    }

    [HttpGet("locksmith-context")]
    public async Task<IActionResult> LocksmithContext(CancellationToken ct) => OkResponse(await _service.GetLocksmithContextAsync(ct));

    [HttpPost("{quoteRequestId:guid}/office-approval")]
    [Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantAdmin)]
    public async Task<IActionResult> OfficeApproval(Guid quoteRequestId, [FromBody] QuoteEstimateVersionRequest request, CancellationToken ct)
        => OkResponse(await _service.ApproveLocksmithPricingAsync(quoteRequestId, request.ExpectedVersion, ct));

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
            ReviewPath(quoteRequestId),
            ct));
}

public sealed class QuoteEstimateVersionRequest
{
    public string? ExpectedVersion { get; set; }
}
