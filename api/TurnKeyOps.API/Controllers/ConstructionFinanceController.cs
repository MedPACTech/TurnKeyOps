using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/finance/construction")][FinanceErrors][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ConstructionFinanceController(ConstructionFinanceService service):ApiControllerBase
{
    [HttpGet]public async Task<IActionResult> Get(DateOnly? asOf,CancellationToken ct)=>OkResponse(await service.WorkspaceAsync(asOf,ct));
    [HttpPost("command")]public async Task<IActionResult> Command(ConstructionFinanceCommand input,CancellationToken ct)=>OkResponse(await service.CommandAsync(input,ct));
}
