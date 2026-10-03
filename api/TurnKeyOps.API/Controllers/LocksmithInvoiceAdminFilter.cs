using MedInsights.Lib;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;

namespace TurnKeyOps.API.Controllers;

// Generic invoice operations remain office-only for the locksmith trade; field access uses scoped routes.
public sealed class LocksmithInvoiceAdminFilter(
    IUserContext user,
    ITenantMembershipRepository memberships,
    ITenantSettingsRepository settings,
    IOptions<QuoteRequestTenantOptions> tenants) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var ct = context.HttpContext.RequestAborted;
        var document = await settings.GetAsync(TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId), "SETTINGS|OPERATIONAL", ct);
        var configuredCarlZipf = tenants.Value.Tenants.TryGetValue("carlzipf", out var tenant) && tenant.TenantId == user.TenantId;
        if (!configuredCarlZipf && (document is null || document.IsDeleted || !LocksmithPolicy.IsConfigured(document.ValuesJson)))
        {
            await next();
            return;
        }
        var member = await memberships.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(user.TenantId), user.UserId, ct);
        if (!user.IsAuthenticated || member is null || member.TenantId != user.TenantId || member.UserId != user.UserId || member.IsDeleted || member.DateRemoved.HasValue ||
            !string.Equals(member.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase) ||
            (!member.IsOwner && !TenantRoleCatalog.CanManageRoles(member.Role.Trim().ToLowerInvariant())))
        {
            context.Result = new ForbidResult();
            return;
        }
        await next();
    }
}
