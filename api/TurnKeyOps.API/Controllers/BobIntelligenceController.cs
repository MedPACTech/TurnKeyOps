using MedInsights.Lib.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services;
namespace TurnKeyOps.API.Controllers;

[Authorize(Policy = TurnKeyAuthorizationPolicies.TenantStaff)]
[Route("api/bob/intelligence")]
public sealed class BobIntelligenceController(BobIntelligenceService service) : ApiControllerBase
{
    [HttpGet("context")] public async Task<IActionResult> Context(string? module, string? entityId, CancellationToken ct) => OkResponse(await service.ContextAsync(module, entityId, ct));
    [HttpGet("briefing")] public async Task<IActionResult> Briefing(CancellationToken ct) => OkResponse(await service.BriefingAsync(ct));
    [HttpGet("capabilities")] public async Task<IActionResult> Capabilities(CancellationToken ct) => OkResponse(await service.CapabilitiesAsync(ct));
    [HttpGet("activity")] public async Task<IActionResult> Activity(CancellationToken ct) => OkResponse(await service.ActivityAsync(ct));
    [HttpPost("command")] public async Task<IActionResult> Command(BobCommandRequest input, CancellationToken ct) => OkResponse(await service.CommandAsync(input, ct));
    [HttpGet("automation")] public async Task<IActionResult> Automation(CancellationToken ct) => OkResponse(await service.AutomationCenterAsync(ct));
    [HttpPut("policy")] public async Task<IActionResult> Policy(BobPolicyUpdate input, CancellationToken ct) { await service.UpdatePolicyAsync(input, ct); return OkResponse(new { updated = true }); }
    [HttpPut("rules")] public async Task<IActionResult> Rule(BobRuleUpdate input, CancellationToken ct) { await service.UpdateRuleAsync(input, ct); return OkResponse(new { updated = true }); }
    [HttpPost("attention/{id}/dismiss")] public async Task<IActionResult> Dismiss(string id, CancellationToken ct) { await service.DismissAsync(id, ct); return OkResponse(new { dismissed = true }); }
    [HttpPost("attention/{id}/watch")] public async Task<IActionResult> Watch(string id, CancellationToken ct) => OkResponse(await service.WatchAsync(id, ct));
    [HttpPost("workflows/{id:guid}/dismiss")] public async Task<IActionResult> DismissWorkflow(Guid id, CancellationToken ct) { await service.DismissWorkflowAsync(id, ct); return OkResponse(new { dismissed = true }); }
    [HttpPost("feedback")] public async Task<IActionResult> Feedback(BobFeedbackRequest input, CancellationToken ct) { await service.FeedbackAsync(input, ct); return OkResponse(new { recorded = true }); }
    [HttpPost("workflows/from-attention")] public async Task<IActionResult> Plan(BobAttentionWorkflowRequest input, CancellationToken ct) => OkResponse(await service.PlanAttentionAsync(input, ct));
    [HttpPost("workflows")] public async Task<IActionResult> Create(BobWorkflowRequest input, CancellationToken ct) => OkResponse(await service.CreateWorkflowAsync(input, ct));
    [HttpPost("workflows/{id:guid}/advance")] public async Task<IActionResult> Advance(Guid id, CancellationToken ct) => OkResponse(await service.AdvanceWorkflowAsync(id, false, ct));
    [HttpPost("workflows/{id:guid}/approve")] public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => OkResponse(await service.AdvanceWorkflowAsync(id, true, ct));
}
