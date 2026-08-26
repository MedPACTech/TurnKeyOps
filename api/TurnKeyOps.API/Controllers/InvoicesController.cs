using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
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

    [HttpPost("sync-approved-estimates")]
    public async Task<IActionResult> SyncApprovedEstimates(CancellationToken ct)
    {
        var result = await _service.SyncApprovedEstimatesAsync(ct);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromBody] InvoiceMutationInputDto input, CancellationToken ct)
    {
        var result = await _service.SendAsync(id, input.ExpectedVersion, ct);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] InvoicePaymentInputDto input, CancellationToken ct)
    {
        input.Kind = "payment";
        var result = await _service.RecordPaymentAsync(id, input, ct);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/refunds")]
    public async Task<IActionResult> RecordRefund(Guid id, [FromBody] InvoicePaymentInputDto input, CancellationToken ct)
    {
        input.Kind = "refund";
        var result = await _service.RecordRefundAsync(id, input, ct);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/reminders")]
    public async Task<IActionResult> RecordReminder(Guid id, [FromBody] InvoiceReminderInputDto input, CancellationToken ct)
    {
        var result = await _service.RecordReminderAsync(id, input, ct);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}/job-release")]
    public async Task<IActionResult> GetJobRelease(Guid id, CancellationToken ct)
    {
        var result = await _service.GetJobReleaseAsync(id, ct);
        return OkResponse(result);
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
