using System.Text.Json;
using MedInsights.Lib.Authorization;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class BobLeadActionProvider(LeadService leads, string toolKey) : IBobActionProvider
{
    public string ToolKey => toolKey;
    public string PermissionKey => toolKey == "lead.summarize" ? TurnKeyPermissionKeys.LeadsRead : TurnKeyPermissionKeys.LeadsWrite;
    public BobActionRisk Risk => toolKey == "lead.summarize" ? BobActionRisk.Read : BobActionRisk.Destructive;
    public async Task<object?> ExecuteAsync(BobActionExecutionContext context, JsonElement input, CancellationToken ct = default)
    {
        var id = input.GetProperty("leadId").GetGuid();
        if (toolKey == "lead.summarize")
        {
            var lead = await leads.GetAsync(id, ct) ?? throw new KeyNotFoundException("Lead not found.");
            return new { lead.Id, lead.Stage, lead.BobSummary, lead.MissingRequired, lead.NextAction };
        }
        var version = input.GetProperty("expectedVersion").GetString() ?? "";
        return toolKey switch
        {
            "lead.assign" => await leads.AssignAsync(id, version, ct),
            "lead.stage" => await leads.StageAsync(id, new() { Stage = input.GetProperty("stage").GetString() ?? "", Reason = input.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "", ExpectedVersion = version }, ct),
            "lead.task" => await leads.NoteAsync(id, new() { Type = "task", Text = input.GetProperty("text").GetString() ?? "", ExpectedVersion = version }, ct),
            "lead.estimate" => await leads.EstimateAsync(id, version, ct),
            "lead.draft" or "lead.send" => await leads.CommunicationAsync(id, new() {
                ExpectedVersion = version, Channel = input.GetProperty("channel").GetString() ?? "email",
                Subject = input.GetProperty("subject").GetString() ?? "", Body = input.GetProperty("body").GetString() ?? ""
            }, toolKey == "lead.send", ct),
            "lead.schedule" => await leads.ScheduleAsync(id, new() { ExpectedVersion = version,
                StartUtc = input.GetProperty("startUtc").GetDateTime(), EndUtc = input.GetProperty("endUtc").GetDateTime() }, ct),
            _ => throw new ArgumentException("Unsupported Lead action.")
        };
    }
}
