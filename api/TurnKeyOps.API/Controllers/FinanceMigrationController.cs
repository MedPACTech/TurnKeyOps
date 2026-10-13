using MedInsights.Lib.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/finance/migration")]
[FinanceErrors]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FinanceMigrationController(FinanceMigrationService migration) : ApiControllerBase
{
    [HttpGet("template")]
    public async Task<IActionResult> Template(CancellationToken ct) => OkResponse(await migration.TemplateAsync(ct));
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(FinanceMigrationRequest request, CancellationToken ct) => OkResponse(await migration.PreviewAsync(request, ct));
    [HttpPost("apply-credit")]
    public async Task<IActionResult> ApplyCredit(FinanceOpeningCreditApplication request, CancellationToken ct) => OkResponse(await migration.ApplyOpeningCreditAsync(request, ct));
    [HttpPost("reverse-credit")]
    public async Task<IActionResult> ReverseCredit(FinanceOpeningCreditReversal request, CancellationToken ct) => OkResponse(await migration.ReverseOpeningCreditAsync(request, ct));
    [HttpPost("import")]
    public async Task<IActionResult> Import(FinanceMigrationRequest request, CancellationToken ct) => OkResponse(await migration.ImportAsync(request, ct));
}
