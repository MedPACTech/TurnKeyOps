using MedInsights.Lib.Authorization;
using MedInsights.Services;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;
public interface IFinanceAuthority
{
    Task RequireAsync(bool write,CancellationToken ct=default);
    Task<bool> CanWriteAsync(CancellationToken ct=default);
    Task<bool> CanControlAsync(CancellationToken ct=default);
}
// Finance grants are independent of Jobs, Purchasing, Invoices and platform Billing.
public sealed class FinanceAuthority(UserModuleAccessService modules,IUserContext user):IFinanceAuthority
{
    public async Task RequireAsync(bool write,CancellationToken ct=default)
    {
        if(!user.IsAuthenticated||user.TenantId==Guid.Empty)throw new UnauthorizedAccessException();
        if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"finance",write))throw new MedInsights.Lib.ForbiddenAccessException("Explicit Finance permission is required.");
    }
    public async Task<bool> CanWriteAsync(CancellationToken ct=default)=>user.IsAuthenticated&&user.TenantId!=Guid.Empty&&UserModulePermissions.Allows(await modules.GetAsync(ct),"finance",true);
    public async Task<bool> CanControlAsync(CancellationToken ct=default)=>await CanWriteAsync(ct)&&await modules.IsOwnerAsync(ct);
}
