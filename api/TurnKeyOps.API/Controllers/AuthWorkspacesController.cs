using System.Security.Claims;
using IBeam.Identity.Interfaces;
using IBeam.Identity.Models;
using MedInsights.Lib.Authorization;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers;

[ApiController]
[Route("api/auth/workspaces")]
[Authorize(Policy = TurnKeyAuthorizationPolicies.AuthenticatedSession)]
public sealed class AuthWorkspacesController(ITenantMembershipRepository memberships, ITenantSelectionService selection) : ControllerBase
{
    private Guid? UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid") ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (UserId is not { } userId) return Unauthorized();
        var items = await memberships.GetByUserIdAsync(userId, ct);
        return Ok(items.Where(m => m.UserId == userId && UserModulePermissions.IsActive(m))
            .Select(m => m.TenantId).Distinct().Select(id => new { tenantId = id }));
    }

    public sealed record SelectRequest(Guid TenantId);

    [HttpPost]
    public async Task<IActionResult> Select(SelectRequest request, CancellationToken ct)
    {
        if (UserId is not { } userId) return Unauthorized();
        var items = await memberships.GetByUserIdAsync(userId, ct);
        if (!items.Any(m => m.UserId == userId && m.TenantId == request.TenantId && UserModulePermissions.IsActive(m)))
            return Forbid();
        // The identity provider independently checks membership and issues a fresh tenant session.
        var token = await selection.SelectTenantAsync(new TenantSelectionRequest(userId, request.TenantId, false), ct);
        return Ok(token);
    }
}
