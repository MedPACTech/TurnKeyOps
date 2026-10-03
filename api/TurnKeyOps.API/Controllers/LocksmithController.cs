using MedInsights.Lib;

using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/locksmith")]
public sealed class LocksmithController(
    IUserContext userContext,
    ITenantMembershipRepository memberships,
    ITenantSettingsRepository settings) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!userContext.IsAuthenticated || userContext.TenantId == Guid.Empty || userContext.UserId == Guid.Empty)
            return Unauthorized();
        var membership = await memberships.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(userContext.TenantId), userContext.UserId, ct);
        if (membership is null || membership.IsDeleted || membership.DateRemoved.HasValue ||
            membership.TenantId != userContext.TenantId || membership.UserId != userContext.UserId ||
            !string.Equals(membership.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase))
            return Forbid();
        var document = await settings.GetAsync(TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(userContext.TenantId), "SETTINGS|OPERATIONAL", ct);
        return OkResponse(new {
            tenantId = userContext.TenantId,
            userId = userContext.UserId,
            capabilities = document is null || document.IsDeleted ? [] : LocksmithPolicy.CapabilitiesFor(document.ValuesJson, membership.Id)
        });
    }
}
