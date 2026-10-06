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
    private readonly TurnKeyOps.Services.IEstimateAuthority? _authority;
    private readonly IOptions<QuoteRequestTenantOptions> _tenants;
    private readonly IUserContext _userContext;
    public QuoteEstimatesController(IQuoteEstimateService service, IOptions<QuoteRequestTenantOptions> tenants, IUserContext userContext, TurnKeyOps.Services.IEstimateAuthority? authority = null)
    { _service = service; _tenants = tenants; _userContext = userContext; _authority = authority; }
    private async Task<IActionResult> PacketResponseAsync(QuoteEstimateDto packet,CancellationToken ct)
        => OkResponse(_authority is not null && !await _authority.CanApproveAsync(ct) ? TurnKeyOps.Services.QuoteEstimateService.CustomerProjection(packet,false) : packet);
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
        => await PacketResponseAsync(await _service.ApproveLocksmithPricingAsync(quoteRequestId, request.ExpectedVersion, ct),ct);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    { var packets=await _service.ListAsync(ct);return OkResponse(_authority is not null && !await _authority.CanApproveAsync(ct) ? packets.Select(x=>TurnKeyOps.Services.QuoteEstimateService.CustomerProjection(x,false)).ToArray() : packets); }

    [HttpGet("{quoteRequestId:guid}")]
    public async Task<IActionResult> Get(Guid quoteRequestId, CancellationToken ct)
    {
        var result = await _service.GetAsync(quoteRequestId, ct);
        return result is null ? NotFound() : OkResponse(_authority is not null && !await _authority.CanApproveAsync(ct) ? TurnKeyOps.Services.QuoteEstimateService.CustomerProjection(result,false) : result);
    }

    [HttpPut("{quoteRequestId:guid}")]
    public async Task<IActionResult> SaveDraft(
        Guid quoteRequestId,
        [FromBody] QuoteEstimateDraftInputDto input,
        CancellationToken ct) => await PacketResponseAsync(await _service.SaveDraftAsync(quoteRequestId, input, ct),ct);

    [HttpPost("{quoteRequestId:guid}/revisions")]
    public async Task<IActionResult> CreateRevision(
        Guid quoteRequestId,
        [FromBody] QuoteEstimateVersionRequest request,
        CancellationToken ct) => await PacketResponseAsync(await _service.CreateRevisionAsync(quoteRequestId, request.ExpectedVersion, ct),ct);

    [HttpPost("{quoteRequestId:guid}/send")]
    public async Task<IActionResult> Send(
        Guid quoteRequestId,
        [FromBody] QuoteEstimateVersionRequest request,
        CancellationToken ct) => await PacketResponseAsync(await _service.SendAsync(
            quoteRequestId,
            request.ExpectedVersion,
            ReviewPath(quoteRequestId),
            ct),ct);
}

public sealed class QuoteEstimateVersionRequest
{
    public string? ExpectedVersion { get; set; }
}
