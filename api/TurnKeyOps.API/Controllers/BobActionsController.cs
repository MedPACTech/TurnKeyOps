using MedInsights.Lib.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/bob")]
public sealed class BobActionsController : ApiControllerBase
{
    private readonly IBobOperationsService _service;

    public BobActionsController(IBobOperationsService service) => _service = service;

    [HttpGet("conversations/{conversationId:guid}/actions")]
    public async Task<IActionResult> List(Guid conversationId, CancellationToken ct) =>
        OkResponse(await _service.ListAsync(conversationId, ct));

    [HttpPost("conversations/{conversationId:guid}/actions")]
    public async Task<IActionResult> Propose(
        Guid conversationId,
        [FromBody] ProposeBobActionDto input,
        CancellationToken ct) =>
        OkResponse(await _service.ProposeAsync(conversationId, input, ct));

    [HttpPost("actions/{actionId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid actionId, CancellationToken ct) =>
        OkResponse(await _service.ApproveAsync(actionId, ct));

    [HttpPost("actions/{actionId:guid}/execute")]
    public async Task<IActionResult> Execute(Guid actionId, CancellationToken ct) =>
        OkResponse(await _service.ExecuteAsync(actionId, ct));
}
