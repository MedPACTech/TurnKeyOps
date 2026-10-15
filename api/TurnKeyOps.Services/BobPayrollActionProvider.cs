using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed class BobPayrollActionProvider(PayrollService service,IUserContext user,string tool):IBobActionProvider
{
    public string ToolKey=>tool;
    public string PermissionKey=>tool=="payroll.readiness"?"payroll.read":"payroll.write";
    public BobActionRisk Risk=>tool=="payroll.readiness"?BobActionRisk.Read:BobActionRisk.Financial;
    public async Task<object?> ExecuteAsync(BobActionExecutionContext context,JsonElement input,CancellationToken ct=default)
    {
        if(context.TenantId!=user.TenantId||context.ActorUserId!=user.UserId)throw new UnauthorizedAccessException();
        if(tool=="payroll.readiness")return await service.ReadinessAsync(ct);
        if(tool!="payroll.prepare")throw new ArgumentException("Unsupported payroll action.");
        var command=input.Deserialize<PayrollCommand>(JobConfigurationService.Json)??throw new ArgumentException("Prepared command required.");if(!input.TryGetProperty("id",out _)||command.Id==Guid.Empty||command.Action!="prepare")throw new ArgumentException("An explicit reviewed prepare command is required. Bob cannot invent payroll inputs.");return await service.CommandAsync(command,ct);
    }
}
