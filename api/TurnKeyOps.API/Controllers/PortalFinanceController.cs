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
}
