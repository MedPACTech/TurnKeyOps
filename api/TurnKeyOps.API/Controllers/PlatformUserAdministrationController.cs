using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers;

[ApiController]
[Route("api/platform/user-administration")]
[Authorize(Policy = TurnKeyAuthorizationPolicies.InternalAdmin)]
public sealed class PlatformUserAdministrationController : ApiControllerBase
{
    private readonly IPlatformUserAdministrationService _service;

    public PlatformUserAdministrationController(IPlatformUserAdministrationService service)
    {
        _service = service;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants(CancellationToken ct) =>
        OkResponse(await _service.GetTenantsAsync(ct));

    [HttpPost("tenants/{tenantKey}/customer-admin-invites")]
    public async Task<IActionResult> CreateCustomerAdminInvite(
        string tenantKey,
        [FromBody] CreateManagedUserInviteRequestDto request,
        CancellationToken ct) =>
        OkResponse(await _service.CreateCustomerAdminInviteAsync(tenantKey, request, ct));
}
