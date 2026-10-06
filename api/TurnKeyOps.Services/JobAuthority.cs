using MedInsights.Lib.Authorization;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;
public interface IJobAuthority
{
    Task RequireAsync(bool write,CancellationToken ct=default);
    Task<bool> CanWriteAsync(CancellationToken ct=default);
    Task RequireCalendarAsync(bool write,CancellationToken ct=default);
    Task<bool> IsOwnerAsync(CancellationToken ct=default);
}
public sealed class JobAuthority(UserModuleAccessService modules,IRoleAccessService roles,IUserContext user):IJobAuthority
{
    public async Task RequireAsync(bool write,CancellationToken ct=default)
    {
        if(!user.IsAuthenticated||user.TenantId==Guid.Empty)throw new UnauthorizedAccessException();
        await roles.RequirePermissionAsync(write?TurnKeyPermissionKeys.JobsWrite:TurnKeyPermissionKeys.JobsRead,ct);
        if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"jobs",write))throw new MedInsights.Lib.ForbiddenAccessException("Jobs permission is required.");
    }
    public async Task<bool> CanWriteAsync(CancellationToken ct=default)=>UserModulePermissions.Allows(await modules.GetAsync(ct),"jobs",true);
    public async Task RequireCalendarAsync(bool write,CancellationToken ct=default)
    {
        await RequireAsync(write,ct);
        await roles.RequirePermissionAsync(write?"calendar.write":"calendar.read",ct);
        if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"calendar",write))throw new MedInsights.Lib.ForbiddenAccessException("Calendar permission is required for scheduling and availability.");
    }
    public Task<bool> IsOwnerAsync(CancellationToken ct=default)=>modules.IsOwnerAsync(ct);
}
