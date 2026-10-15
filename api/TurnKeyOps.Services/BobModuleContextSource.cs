using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;

public sealed record BobModuleContextResult(IReadOnlyList<BobContextFact> Facts, bool Complete = true);
public interface IBobContextSource
{
    string[] Modules { get; }
    Task<BobModuleContextResult> ReadAsync(string module, string? entityId, DateTime now, CancellationToken ct);
}
// Deliberate projections of module service contracts. No domain copies or LLM-generated facts.
public sealed class BobModuleContextSource(LeadService leads, IJobService jobs, IQuoteEstimateService estimates,
    ICalendarEventService calendar, SupplyService supply, FinanceService finance, PayrollService payroll) : IBobContextSource
{
    public string[] Modules => ["leads", "jobs", "estimates", "calendar", "inventory", "purchasing", "finance", "payroll"];
    public async Task<BobModuleContextResult> ReadAsync(string module, string? entityId, DateTime now, CancellationToken ct)
    {
        var facts = new List<BobContextFact>(); var complete = true;
        void Add(string id, string condition, string title, string explanation, string next, AttentionSeverity severity,
            DateTime? due = null, string? capability = null, string version = "", string trade = "general")
        { if (entityId is null || entityId == id) facts.Add(new(new(module, id, condition, version), title, explanation, next, severity, due, capability, trade)); }
        if (module == "leads")
        {
            var records = entityId is null ? (await leads.WorkspaceAsync(ct)).Leads : new[] { await leads.GetAsync(Guid.Parse(entityId), ct) }.OfType<LeadDto>().ToArray();
            complete = records.Count() < 60;
            foreach (var l in records.Where(l => !LeadStages.IsClosed(l.Stage)).OrderBy(l => l.FollowUpAtUtc ?? l.CreatedAtUtc).Take(60))
            {
                var overdue = l.FollowUpAtUtc < now || l.FirstResponseAtUtc is null && l.CreatedAtUtc.AddHours(4) < now;
                var missing = l.MissingRequired.Count > 0;
                Add(l.Id.ToString(), overdue ? "follow-up-overdue" : missing ? "missing-qualification" : "next-step", l.Title,
                    overdue ? "The recorded response or follow-up deadline has passed." : l.BobSummary,
                    missing ? "Complete qualification" : l.NextAction, overdue ? AttentionSeverity.WARNING : AttentionSeverity.ACTION,
                    l.FollowUpAtUtc ?? l.CreatedAtUtc.AddHours(4), "lead.summarize", l.Version, l.TradeProfile);
            }
        }
        if (module == "jobs")
        {
            var records = entityId is null ? (await jobs.GetPagedAsync(60, null, ct)).Items : new[] { await jobs.GetAsync(Guid.Parse(entityId), ct) }.OfType<JobDto>();
            complete = records.Count() < 60;
            foreach (var j in records)
            {
                var state = JobExecutionRules.State(j.Status);
                if (state is "COMPLETED" or "CLOSED" or "CANCELLED") continue;
                var blockers = j.Execution is null ? [] : JobExecutionRules.Blockers(j.Execution, state is "PLANNING" or "READY_TO_SCHEDULE" ? "schedule" : "start");
                Add(j.Id.ToString(), blockers.Count > 0 ? "readiness-blocked" : "next-step", j.Name,
                    blockers.Count > 0 ? string.Join("; ", blockers.Take(4)) : $"Recorded state: {state}.",
                    blockers.Count > 0 ? "Resolve the recorded readiness blockers" : JobExecutionRules.NextAction(state),
                    blockers.Count > 0 ? AttentionSeverity.WARNING : AttentionSeverity.ACTION, j.ScheduledStart, "job.readiness", j.Version, j.Execution?.TradeProfile ?? "general");
            }
        }
        if (module == "estimates")
        {
            var records = entityId is null ? await estimates.ListAsync(ct) : new[] { await estimates.GetAsync(Guid.Parse(entityId), ct) }.OfType<QuoteEstimateDto>().ToArray();
            complete = records.Count() < 60;
            foreach (var e in records.Where(e => e.Status is not ("accepted" or "declined" or "superseded")).OrderBy(e => e.SavedAtUtc).Take(60))
            {
                var stalled = e.SentAtUtc is DateTime sent && sent.AddDays(3) < now;
                var blockers = e.Pricing?.Blockers ?? [];
                Add(e.QuoteRequestId.ToString(), blockers.Count > 0 ? "pricing-blocked" : stalled ? "proposal-no-response" : "next-step", e.SiteName.Length > 0 ? e.SiteName : e.CustomerName,
                    blockers.Count > 0 ? string.Join("; ", blockers.Take(4)) : stalled ? "Proposal has been sent for at least three days without a recorded outcome." : $"Estimate is {e.Status}.",
                    blockers.Count > 0 ? "Review pricing requirements" : stalled ? "Review and draft a follow-up" : "Review the estimate",
                    stalled || blockers.Count > 0 ? AttentionSeverity.WARNING : AttentionSeverity.ACTION, e.SentAtUtc?.AddDays(3), "estimate.summarize", e.Version, e.Document?.TradeProfile ?? "general");
            }
        }
        if (module == "calendar")
        {
            var events = (await calendar.GetByDateRangeAsync(now.Date, now.Date.AddDays(3))).Take(100).ToArray();
            complete = events.Length < 100;
            foreach (var e in events.Where(e => e.EventStatus != "cancelled"))
                Add(e.Id.ToString(), "upcoming-appointment", e.Title, $"Scheduled {e.StartUtc:u} to {e.EndUtc:u}.", "Review appointment and resource readiness", AttentionSeverity.INFO, e.StartUtc);
        }
        if (module is "inventory" or "purchasing") facts.AddRange(await supply.BobIntelligenceAsync(module, entityId, now, ct));
        if (module == "finance") facts.AddRange(await finance.BobIntelligenceAsync(entityId, now, ct));
        if (module == "payroll")
        {
            var data = JsonSerializer.SerializeToElement(await payroll.ReadinessAsync(ct), JobConfigurationService.Json);
            foreach (var e in data.GetProperty("exceptions").EnumerateArray().Take(60))
                Add(e.GetProperty("id").GetString()!, "time-exception", "Time needs review", string.Join("; ", e.GetProperty("issues").EnumerateArray().Select(i => i.GetString())), "Review source time and resolve exceptions", AttentionSeverity.WARNING, capability: "payroll.readiness");
        }
        return new(facts.Take(60).ToArray(), complete && facts.Count < 60);
    }
}
