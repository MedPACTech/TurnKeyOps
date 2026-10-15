using System.Security.Cryptography;
using System.Text;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

public static class BobAttentionEngine
{
    public static string Key(Guid actor, BobSourceReference source) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{actor:N}|{source.Module}|{source.EntityId}|{source.Condition}"))).ToLowerInvariant();
    public static void Reconcile(BobIntelligenceState state, BobOperatingContext context, string? focusedEntity = null)
    {
        var seen = new HashSet<string>();
        foreach (var fact in context.Facts.Where(f => f.Severity >= AttentionSeverity.ACTION))
        {
            var id = Key(context.ActorUserId, fact.Source); seen.Add(id);
            var item = state.Attention.SingleOrDefault(a => a.Id == id);
            if (item is null)
            {
                item = new() { Id = id, ActorUserId = context.ActorUserId, Source = fact.Source, FirstObservedUtc = context.AsOfUtc };
                state.Attention.Add(item);
            }
            // Dismissal persists for an unchanged active condition; a resolved condition can recur.
            if (item.Status == "resolved") { item.Status = "open"; item.FirstObservedUtc = context.AsOfUtc; item.ResolvedUtc = null; item.ResolutionSource = null; }
            item.LastObservedUtc = context.AsOfUtc; item.Source = fact.Source; item.Title = fact.Title;
            item.Explanation = fact.Explanation; item.Reason = fact.Source.Condition;
            item.RecommendedAction = fact.NextAction; item.CapabilityId = fact.CapabilityId; item.DueUtc = fact.DueUtc;
            item.Severity = fact.Severity; item.EscalatedTo = null;
            foreach (var rule in state.Policy.Escalations.Where(r => r.Module == fact.Source.Module && r.Condition == fact.Source.Condition)
                .OrderBy(r => r.AfterHours))
                if (context.AsOfUtc >= (fact.DueUtc ?? item.FirstObservedUtc).AddHours(rule.AfterHours))
                { item.Severity = (AttentionSeverity)Math.Max((int)item.Severity, (int)rule.Severity); item.EscalatedTo = rule.Team; }
        }
        foreach (var item in state.Attention.Where(a => a.ActorUserId == context.ActorUserId && a.Status != "resolved" && !seen.Contains(a.Id)))
        {
            // Never resolve a condition when its module is disabled, forbidden, failed, or retrieval is bounded.
            if (!context.Modules.Contains(item.Source.Module) || context.UnavailableModules.Contains(item.Source.Module) || (context.IncompleteModules?.Contains(item.Source.Module) ?? false) ||
                context.Facts.Count(f => f.Source.Module == item.Source.Module) >= 60 ||
                focusedEntity is not null && focusedEntity != item.Source.EntityId) continue;
            item.Status = "resolved"; item.ResolvedUtc = context.AsOfUtc; item.ResolutionSource = "authoritative module check";
        }
    }
    public static int RolePriority(string role, string module) => role.ToLowerInvariant() switch {
        "sales" or "estimator" => module is "leads" or "estimates" ? 0 : 2,
        "dispatch" or "scheduler" => module is "calendar" or "jobs" ? 0 : 2,
        "foreman" or "field" or "technician" => module is "jobs" or "calendar" or "payroll" ? 0 : 2,
        "bookkeeper" or "finance" => module == "finance" ? 0 : 2,
        "payroll" => module == "payroll" ? 0 : 2,
        "operations" or "project_manager" => module is "jobs" or "inventory" or "purchasing" or "calendar" ? 0 : 2,
        _ => 1
    };
}
