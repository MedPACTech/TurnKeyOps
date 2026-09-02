using MedInsights.Lib.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = TurnKeyAuthorizationPolicies.TenantAdmin)]
[Route("api/admin/contact-access")]
public sealed class AdminContactAccessController : ApiControllerBase
{
    private readonly IContactAccessGrantService _service;

    public AdminContactAccessController(IContactAccessGrantService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _service.ListAsync(ct);
        return OkResponse(result);
    }

    [HttpGet("{contactId}")]
    public async Task<IActionResult> Get(string contactId, CancellationToken ct)
    {
        var result = await _service.GetAsync(contactId, ct);
        return result is null ? NotFound() : OkResponse(result);
    }

    [HttpPut("{contactId}")]
    public async Task<IActionResult> Put(
        string contactId,
        [FromBody] UpdateContactAccessGrantDto input,
        CancellationToken ct)
    {
        var result = await _service.UpsertAsync(contactId, input, ct);
        return OkResponse(result);
    }
}
