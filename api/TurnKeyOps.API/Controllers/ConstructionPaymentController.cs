using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.API.Controllers;
[Authorize(Policy=MedInsights.Lib.Authorization.TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/finance/construction/payments")][FinanceErrors][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ConstructionPaymentController(ConstructionPaymentService service):ApiControllerBase
{
 [HttpGet]public async Task<IActionResult> Get(CancellationToken ct)=>OkResponse(await service.ReadAsync(ct));
 [HttpPost("command")]public async Task<IActionResult> Command(ConstructionPaymentCommand input,CancellationToken ct)=>OkResponse(await service.CommandAsync(input,ct));
 [AllowAnonymous][HttpPost("stripe-webhook")]public async Task<IActionResult> Webhook(CancellationToken ct)
 {using var reader=new StreamReader(Request.Body);var json=await reader.ReadToEndAsync(ct);var headers=Request.Headers.ToDictionary(h=>h.Key,h=>h.Value.ToString(),StringComparer.OrdinalIgnoreCase);return OkResponse(await service.ReceiveAsync(json,headers,ct));}
}
