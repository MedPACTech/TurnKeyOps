using MedInsights.Lib.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/admin/tenant-settings")]
public sealed class AdminTenantSettingsController : ApiControllerBase
{
    private readonly ITenantSettingsService _service;

    public AdminTenantSettingsController(ITenantSettingsService service)
    {
        _service = service;
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> Get(string kind, CancellationToken ct)
    {
        var result = await _service.GetProtectedAsync(kind, ct);
        return OkResponse(result);
    }

    [HttpPut("{kind}")]
    [Authorize(Policy = TurnKeyAuthorizationPolicies.TenantAdmin)]
    public async Task<IActionResult> Put(
        string kind,
        [FromBody] UpdateTenantSettingsDocumentDto input,
        CancellationToken ct)
    {
        var result = await _service.UpsertAsync(kind, input, ct);
        return OkResponse(result);
    }
}
