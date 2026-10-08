using System.Text.Json;
using MedInsights.Lib.Authorization;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;
public sealed class BobEstimateActionProvider(QuoteEstimateService service,EstimateDeliveryService delivery,string tool):IBobActionProvider
{
    public string ToolKey=>tool;
    public string PermissionKey=>tool=="estimate.summarize"?TurnKeyPermissionKeys.EstimatesRead:TurnKeyPermissionKeys.EstimatesWrite;
    public BobActionRisk Risk=>tool=="estimate.summarize"?BobActionRisk.Read:BobActionRisk.Destructive;
    public async Task<object?> ExecuteAsync(BobActionExecutionContext context,JsonElement input,CancellationToken ct=default)
    {
        var id=input.GetProperty("estimateId").GetGuid();var workspace=await service.WorkspaceAsync(id,ct);
        if(tool=="estimate.summarize")return new{workspace.State,workspace.NextAction,workspace.Packet.Pricing?.Blockers,workspace.Packet.Pricing?.ApprovalReasons};
        var version=input.GetProperty("expectedVersion").GetString()??"";
        var text=input.TryGetProperty("text",out var value)?value.GetString()??"":"";
        _ = tool switch{
            "estimate.extract"=>await service.StructureAsync(id,new(){ExpectedVersion=version,Text=text},ct),
            "estimate.price"=>await service.PriceWorkspaceAsync(id,new(){ExpectedVersion=version,Document=workspace.Packet.Document??throw new ArgumentException("Common draft required."),DiscountPercent=workspace.Packet.Pricing?.DiscountPercent??0},ct),
            "estimate.revise"=>await service.CreateRevisionAsync(id,version,ct),
            "estimate.issue"=>await service.IssueAsync(id,version,ct),
            "estimate.remind"=>await service.DeliverAsync(id,new(){ExpectedVersion=version,Text=text,Channel=input.TryGetProperty("channel",out var channel)?channel.GetString()??"email":"email"},delivery,ct),
            _=>throw new ArgumentException("Unknown estimating action.")
        };
        return await service.WorkspaceAsync(id,ct);
    }
}
