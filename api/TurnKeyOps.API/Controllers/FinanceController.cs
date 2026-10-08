using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/finance")][FinanceErrors][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class FinanceController(FinanceService service,IFinanceAuthority authority,TurnKeyOps.Lib.Utils.IUserContext user,TurnKeyOps.Services.Interfaces.IBobOperationsService bob):ApiControllerBase
{
    [HttpPost("bob")]public async Task<IActionResult> Bob(ProposeBobActionDto input,CancellationToken ct)
    {
        await authority.RequireAsync(input.ToolKey=="finance.draft-journal",ct);
        var args=System.Text.Json.Nodes.JsonNode.Parse(input.Input.GetRawText())!.AsObject();args["financeId"]=user.TenantId.ToString();input.Input=System.Text.Json.JsonSerializer.SerializeToElement(args);
        return OkResponse(await bob.ProposeFinanceAsync(user.TenantId,input,ct));
    }
    [HttpPost("bob/{actionId:guid}/approve")]public async Task<IActionResult> ApproveBob(Guid actionId,CancellationToken ct)
    {await authority.RequireAsync(true,ct);return OkResponse(await bob.ApproveFinanceAsync(user.TenantId,actionId,ct));}
    [HttpGet]public async Task<IActionResult> Get(DateOnly? from,DateOnly? asOf,CancellationToken ct)=>OkResponse(await service.WorkspaceAsync(from,asOf,ct));
    [HttpPost("command")]public async Task<IActionResult> Command(FinanceCommand input,CancellationToken ct)=>OkResponse(await service.CommandAsync(input,ct));
}
public sealed class FinanceErrorsAttribute:ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var (status,message)=context.Exception switch{ArgumentException e=>(400,e.Message),KeyNotFoundException=>(404,"Financial record not found."),InvalidOperationException=>(409,"Finance changed or storage is unavailable. Refresh before retrying; no successful change is confirmed."),_=>(0,"")};
        if(status==0)return;context.Result=new ObjectResult(new{message}){StatusCode=status};context.ExceptionHandled=true;
    }
}
