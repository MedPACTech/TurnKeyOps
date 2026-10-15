using MedInsights.Lib.Authorization;
using MedInsights.Services;
using TurnKeyOps.Lib.Utils;
namespace TurnKeyOps.Services;
public interface IPayrollAuthority
{
    Task RequireAsync(bool write,CancellationToken ct=default);
    Task<bool> AllowsAsync(string action,CancellationToken ct=default);
}
public sealed class PayrollAuthority(UserModuleAccessService modules,IUserContext user):IPayrollAuthority
{
    public async Task RequireAsync(bool write,CancellationToken ct=default){if(!user.IsAuthenticated||user.TenantId==Guid.Empty)throw new UnauthorizedAccessException();if(!UserModulePermissions.Allows(await modules.GetAsync(ct),"payroll",write))throw new MedInsights.Lib.ForbiddenAccessException("Explicit Payroll & Time permission required.");}
    public async Task<bool> AllowsAsync(string action,CancellationToken ct=default){await RequireAsync(false,ct);var grants=await modules.GetAsync(ct);return grants.Contains(action)&&(action=="payroll.compensation.read"||UserModulePermissions.Allows(grants,"payroll",true));}
}
