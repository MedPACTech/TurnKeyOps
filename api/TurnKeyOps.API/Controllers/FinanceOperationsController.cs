using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/finance/operations")][FinanceErrors][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class FinanceOperationsController(FinanceOperationsService service):ApiControllerBase
{
    [HttpGet]public async Task<IActionResult> Get(int? year,CancellationToken ct)=>OkResponse(await service.ReadAsync(ct,year));
    [HttpPost("command")]public async Task<IActionResult> Command(FinanceOperationsCommand input,CancellationToken ct)=>OkResponse(await service.CommandAsync(input,ct));
}
