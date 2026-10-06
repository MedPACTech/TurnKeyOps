using MedInsights.Lib.Authorization;
using MedInsights.Services;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services;
public interface IEstimateAuthority
{
    Task RequireAsync(bool write, CancellationToken ct);
    Task<bool> CanWriteAsync(CancellationToken ct);
    Task<bool> CanApproveAsync(CancellationToken ct);
}
public sealed class EstimateAuthority(UserModuleAccessService modules, IUserContext user) : IEstimateAuthority
{
    public async Task RequireAsync(bool write,CancellationToken ct)
    {
        if(!user.IsAuthenticated||user.TenantId==Guid.Empty)throw new UnauthorizedAccessException();
        if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"estimates",write))throw new MedInsights.Lib.ForbiddenAccessException("Estimates access is required for this action.");
    }
    public async Task<bool> CanWriteAsync(CancellationToken ct)=>UserModulePermissions.Allows(await modules.GetAsync(ct),"estimates",true);
    public Task<bool> CanApproveAsync(CancellationToken ct)=>modules.IsOwnerAsync(ct);
}
