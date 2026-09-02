using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[AllowAnonymous]
[Route("api/public/tenant-settings")]
public sealed class PublicTenantSettingsController : ApiControllerBase
{
    private readonly ITenantSettingsService _service;

    public PublicTenantSettingsController(ITenantSettingsService service)
    {
        _service = service;
    }

    [HttpGet("{tenantId:guid}/content")]
    public async Task<IActionResult> GetContent(Guid tenantId, CancellationToken ct)
    {
        var result = await _service.GetPublicAsync(tenantId, ct);
        return OkResponse(result);
    }
}
