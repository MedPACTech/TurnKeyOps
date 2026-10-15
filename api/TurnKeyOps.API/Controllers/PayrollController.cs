using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/payroll")][PayrollErrors][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PayrollController(PayrollService service):ApiControllerBase
{
    [HttpPost("bob")]public async Task<IActionResult> Bob(ProposeBobActionDto input,[FromServices]TurnKeyOps.Services.Interfaces.IBobOperationsService bob,[FromServices]TurnKeyOps.Lib.Utils.IUserContext user,[FromServices]IPayrollAuthority authority,CancellationToken ct){await authority.RequireAsync(input.ToolKey!="payroll.readiness",ct);return OkResponse(await bob.ProposePayrollAsync(user.TenantId,input,ct));}
    [HttpPost("bob/{id:guid}/approve")]public async Task<IActionResult> ApproveBob(Guid id,[FromServices]TurnKeyOps.Services.Interfaces.IBobOperationsService bob,[FromServices]TurnKeyOps.Lib.Utils.IUserContext user,[FromServices]IPayrollAuthority authority,CancellationToken ct){await authority.RequireAsync(true,ct);return OkResponse(await bob.ApprovePayrollAsync(user.TenantId,id,ct));}
    [HttpGet]public async Task<IActionResult> Get(CancellationToken ct)=>OkResponse(await service.ReadAsync(ct));
    [HttpPost("command")]public async Task<IActionResult> Command(PayrollCommand input,CancellationToken ct)=>OkResponse(await service.CommandAsync(input,ct));
    [HttpGet("export/{id:guid}")]public async Task<IActionResult> Export(Guid id,CancellationToken ct)=>File(System.Text.Encoding.UTF8.GetBytes(await service.ExportAsync(id,ct)),"text/csv; charset=utf-8",$"payroll-{id}.csv");
    [HttpGet("readiness")]public async Task<IActionResult> Readiness(CancellationToken ct)=>OkResponse(await service.ReadinessAsync(ct));
}
public sealed class PayrollErrorsAttribute:ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context){var(status,message)=context.Exception switch{ArgumentException e=>(400,e.Message),TimeZoneNotFoundException=>(400,"Unknown timezone."),InvalidOperationException=>(409,"Payroll changed or is unavailable. Refresh before retrying; no successful change is confirmed."),_=>(0,"")};if(status==0)return;context.Result=new ObjectResult(new{message}){StatusCode=status};context.ExceptionHandled=true;}
}
