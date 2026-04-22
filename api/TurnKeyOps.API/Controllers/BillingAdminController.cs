using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("billing/admin")]
[Authorize]
public class BillingAdminController : MedInsights.Controllers.ApiControllerBase
{
    private readonly IBillingAdminService _service;

    public BillingAdminController(IBillingAdminService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
        => OkResponse(await _service.GetSummaryAsync(ct));

    [HttpGet("ledger")]
    public async Task<IActionResult> GetBillingLedger([FromQuery] int take = 100, CancellationToken ct = default)
        => OkResponse(await _service.GetBillingLedgerAsync(take, ct));

    [HttpGet("credits/ledger")]
    public async Task<IActionResult> GetCreditLedger([FromQuery] int take = 100, CancellationToken ct = default)
        => OkResponse(await _service.GetCreditLedgerAsync(take, ct));

    [HttpGet("credits/periods")]
    public async Task<IActionResult> GetCreditPeriods([FromQuery] int take = 100, CancellationToken ct = default)
        => OkResponse(await _service.GetCreditPeriodsAsync(take, ct));

    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits(CancellationToken ct)
    {
        var view = await _service.GetCreditViewAsync(ct);
        return view is null ? NotFound() : OkResponse(view);
    }

    [HttpGet("topups/settings")]
    public async Task<IActionResult> GetTopUpSettings(CancellationToken ct)
    {
        var settings = await _service.GetTopUpSettingsAsync(ct);
        return settings is null ? NotFound() : OkResponse(settings);
    }

    [HttpPut("topups/settings")]
    public async Task<IActionResult> UpdateTopUpSettings([FromBody] TenantBillingAccountDto dto, CancellationToken ct)
        => OkResponse(await _service.UpdateTopUpSettingsAsync(dto, ct));

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
        => OkResponse(await _service.GetTenantUsersAsync(ct));

    [HttpGet("invites")]
    public async Task<IActionResult> GetInvites(CancellationToken ct)
        => OkResponse(await _service.GetInvitesAsync(ct));

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit([FromQuery] int take = 100, CancellationToken ct = default)
        => OkResponse(await _service.GetAuditEventsAsync(take, ct));

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] string? status = null, [FromQuery] int take = 100, CancellationToken ct = default)
        => OkResponse(await _service.GetOperationalAlertsAsync(status, take, ct));

    [HttpPost("alerts/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id, CancellationToken ct)
        => OkResponse(await _service.AcknowledgeOperationalAlertAsync(id, ct));

    [HttpGet("seats")]
    public async Task<IActionResult> GetSeats(CancellationToken ct)
    {
        var view = await _service.GetSeatViewAsync(ct);
        return view is null ? NotFound() : OkResponse(view);
    }

    [HttpPost("invites/reconcile")]
    public async Task<IActionResult> ReconcileInvites([FromQuery] bool apply = false, CancellationToken ct = default)
        => OkResponse(await _service.ReconcileInviteStateAsync(apply, ct));
}
