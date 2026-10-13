using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.API.Controllers;
[ApiController,Route("api/portal/{slug}/finance"),AllowAnonymous,PortalErrors]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PortalFinanceController(PortalAccessService access,IQuoteRequestTenantResolver tenants,PortalFinanceService finance):ControllerBase
{
    [HttpGet]public async Task<IActionResult> Get(string slug,CancellationToken ct)
    {
        var actor=await access.AuthenticateAsync(tenants.Resolve(slug).TenantId,Request.Headers["X-Portal-Session"].FirstOrDefault(),ct);
        return Ok(await finance.ListAsync(actor,ct));
    }
    [HttpGet("construction")]public async Task<IActionResult> Construction(string slug,CancellationToken ct)
    {var actor=await access.AuthenticateAsync(tenants.Resolve(slug).TenantId,Request.Headers["X-Portal-Session"].FirstOrDefault(),ct);return Ok(await finance.ConstructionAsync(actor,ct));}
    [HttpPost("construction/{applicationId:guid}/approve")]public async Task<IActionResult> ApproveConstruction(string slug,Guid applicationId,TurnKeyOps.Lib.Dtos.ConstructionPortalApproval input,CancellationToken ct)
    {var actor=await access.AuthenticateAsync(tenants.Resolve(slug).TenantId,Request.Headers["X-Portal-Session"].FirstOrDefault(),ct);await finance.ApproveConstructionAsync(actor,applicationId,input,ct);return NoContent();}
}
