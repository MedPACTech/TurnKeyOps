using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
// Financial control actions are intentionally absent: Bob can analyze or prepare an unposted draft.
public sealed class BobFinanceActionProvider(FinanceService service,TurnKeyOps.Lib.Utils.IUserContext user,string tool):IBobActionProvider
{
    public string ToolKey=>tool;
    public string PermissionKey=>tool=="finance.draft-journal"?"finance.write":"finance.read";
    public BobActionRisk Risk=>tool=="finance.draft-journal"?BobActionRisk.Financial:BobActionRisk.Read;
    public async Task<object?> ExecuteAsync(BobActionExecutionContext context,JsonElement input,CancellationToken ct=default)
    {
        if(context.TenantId!=user.TenantId||context.ActorUserId!=user.UserId)throw new UnauthorizedAccessException();
        if(tool!="finance.draft-journal")return await service.WorkspaceAsync(ct:ct);
        var command=input.Deserialize<FinanceCommand>(JobConfigurationService.Json)??throw new ArgumentException("Explicit journal draft required.");
        command.Action="draft-journal";return await service.CommandAsync(command,ct);
    }
}
