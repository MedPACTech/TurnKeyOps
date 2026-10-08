using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed class BobSupplyActionProvider(SupplyService service,string tool,SupplyOrderDelivery? delivery=null):IBobActionProvider
{
    public string ToolKey=>tool;
    public string PermissionKey=>tool switch{"supply.purchasing"=>"purchasing.read","supply.request" or "supply.draft-order" or "supply.send"=>"purchasing.write","supply.reserve"=>"inventory.write",_=>"inventory.read"};
    public BobActionRisk Risk=>tool=="supply.send"?BobActionRisk.Financial:PermissionKey.EndsWith(".read")?BobActionRisk.Read:BobActionRisk.Destructive;
    public async Task<object?> ExecuteAsync(BobActionExecutionContext context,JsonElement input,CancellationToken ct=default)
    {
        var module=PermissionKey.Split('.')[0];
        if(Risk==BobActionRisk.Read)return await service.WorkspaceAsync(module,ct);
        var command=input.Deserialize<SupplyCommand>(JobConfigurationService.Json)??throw new ArgumentException("Supply command required.");
        if(tool=="supply.send")return await (delivery??throw new InvalidOperationException("Vendor transport is unavailable.")).SendAsync(command.TargetId,command.ExpectedVersion,ct);
        command.Action=tool switch{"supply.reserve"=>"reserve","supply.request"=>"request","supply.draft-order"=>"draft-order",_=>throw new ArgumentException("Unsupported supply action.")};
        return await service.CommandAsync(module,command,ct);
    }
}
