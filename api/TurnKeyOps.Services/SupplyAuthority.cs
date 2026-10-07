using MedInsights.Lib.Authorization;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;
public interface ISupplyAuthority
{
    Task RequireAsync(string module,bool write,CancellationToken ct=default);
    Task<bool> CanAsync(string module,bool write,CancellationToken ct=default);
    Task<bool> OwnerAsync(CancellationToken ct=default);
}
public sealed class SupplyAuthority(UserModuleAccessService modules,IRoleAccessService roles,IUserContext user):ISupplyAuthority
{
    public async Task RequireAsync(string module,bool write,CancellationToken ct=default)
    {if(!await CanAsync(module,write,ct))throw new MedInsights.Lib.ForbiddenAccessException($"{module}.{(write?"write":"read")} permission is required.");}
    public async Task<bool> CanAsync(string module,bool write,CancellationToken ct=default)=>user.IsAuthenticated&&user.TenantId!=Guid.Empty&&UserModulePermissions.Allows(await modules.GetAsync(ct),module,write)&&await roles.HasPermissionAsync(module+(write?".write":".read"),ct);
    public Task<bool> OwnerAsync(CancellationToken ct=default)=>modules.IsOwnerAsync(ct);
}
