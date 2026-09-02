using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
public class EstimatesController : ApiControllerBase
{
    private readonly IEstimateService _service;

    public EstimatesController(IEstimateService service) => _service = service;

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
    public async Task<IActionResult> Create([FromBody] EstimateDto dto)
    {
        var result = await _service.AddAsync(dto);
        return CreatedResponse(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("from-appointment")]
    public async Task<IActionResult> CreateFromAppointment([FromBody] CreateEstimateFromAppointmentRequestDto dto)
    {
        var result = await _service.CreateFromAppointmentAsync(dto);
        return CreatedResponse(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] EstimateDto dto)
    {
        var result = await _service.UpdateAsync(dto);
        return OkResponse(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStructured(Guid id, [FromBody] UpdateEstimateStructuredRequestDto dto)
    {
        var result = await _service.UpdateStructuredAsync(id, dto);
        return OkResponse(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContentResponse();
    }

    /// <summary>Create estimate from a template.</summary>
    [HttpPost("from-template")]
    public async Task<IActionResult> CreateFromTemplate([FromQuery] Guid templateId, [FromQuery] Guid customerId, [FromQuery] Guid? jobId = null)
    {
        var result = await _service.CreateFromTemplateAsync(templateId, customerId, jobId);
        return OkResponse(result);
    }

    /// <summary>Concrete CY calculator.</summary>
    [HttpPost("calculate/concrete")]
    public async Task<IActionResult> CalculateConcrete([FromBody] ConcreteCalculatorRequest request)
    {
        var result = await _service.CalculateConcreteAsync(request);
        return OkResponse(result);
    }

    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] StructuredEstimateInputDto request)
    {
        var result = await _service.CalculateAsync(request);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var result = await _service.SubmitAsync(id);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/under-review")]
    public async Task<IActionResult> StartReview(Guid id)
    {
        var result = await _service.StartReviewAsync(id);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/award")]
    public async Task<IActionResult> Award(Guid id)
    {
        var result = await _service.AwardAsync(id);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var result = await _service.RejectAsync(id);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<IActionResult> Revise(Guid id)
    {
        var result = await _service.ReviseAsync(id);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/convert-to-job")]
    public async Task<IActionResult> ConvertToJob(Guid id)
    {
        var result = await _service.ConvertToJobAsync(id);
        return OkResponse(result);
    }

    /// <summary>E-sign an estimate.</summary>
    [HttpPost("{id:guid}/sign")]
    public async Task<IActionResult> Sign(Guid id, [FromBody] SignEstimateRequest request)
    {
        var result = await _service.SignAsync(id, request.SignatureDataUrl, request.SignedByName);
        return OkResponse(result);
    }
}

public class SignEstimateRequest
{
    public string SignatureDataUrl { get; set; } = string.Empty;
    public string SignedByName { get; set; } = string.Empty;
}
