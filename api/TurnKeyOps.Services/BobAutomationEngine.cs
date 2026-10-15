using System.Security.Cryptography;
using System.Text;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;

// Operational events contain references and deterministic condition metadata, never arbitrary executable instructions.
public sealed record BobConditionEvent(string Id, Guid TenantId, Guid ActorUserId, string Type,
    BobSourceReference Source, AttentionSeverity Severity, DateTime ObservedUtc);
public static class BobAutomationEngine
{
    public static IReadOnlyList<BobConditionEvent> Events(BobIntelligenceState state, BobOperatingContext context)
    {
        var events = new List<BobConditionEvent>();
        foreach (var fact in context.Facts.Where(f => f.Severity >= AttentionSeverity.ACTION))
        {
            var previous = state.Attention.SingleOrDefault(a => a.Id == BobAttentionEngine.Key(context.ActorUserId, fact.Source));
            if (previous is not null && previous.Status != "resolved" && previous.Source.Version == fact.Source.Version && previous.Explanation == fact.Explanation && previous.Severity == fact.Severity) continue;
            var type = previous is null || previous.Status == "resolved" ? "ConditionObserved" : "ConditionChanged";
            var fingerprint = $"{context.TenantId:N}|{context.ActorUserId:N}|{fact.Source.Module}|{fact.Source.EntityId}|{fact.Source.Condition}|{fact.Source.Version}|{fact.Explanation}|{type}";
            events.Add(new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))), context.TenantId, context.ActorUserId, type, fact.Source, fact.Severity, context.AsOfUtc));
        }
        return events;
    }
    public static bool Matches(BobAutomationRule rule, BobConditionEvent e) => rule.Enabled && rule.OwnerUserId == e.ActorUserId &&
        (rule.EffectiveUtc is null || rule.EffectiveUtc <= e.ObservedUtc) && rule.Module == e.Source.Module && rule.Condition == e.Source.Condition && e.Severity >= rule.MinimumSeverity;
    public static void Handle(BobIntelligenceState state, BobOperatingContext context, IReadOnlyList<BobConditionEvent> events)
    {
        if (events.Any(e => e.TenantId != context.TenantId || e.ActorUserId != context.ActorUserId || !context.Modules.Contains(e.Source.Module))) throw new UnauthorizedAccessException("Operational event scope mismatch.");
        foreach (var e in events)
        foreach (var rule in state.Rules.Where(r => Matches(r, e)).OrderBy(r => r.Priority))
        {
            if (rule.Action != "attention") throw new InvalidOperationException("Unregistered automation action.");
            var key = $"rule:{rule.Id:N}:{e.Id}";
            if (state.Events.Any(x => x.TargetId == key)) continue;
            // Attention is reconciled from authoritative context immediately after the handlers.
            state.Events.Add(new(Guid.NewGuid(), context.ActorUserId, context.AsOfUtc, "automation.attention", key,
                "A configured rule matched an authorized source condition; attention was reconciled", e.Source.Module));
        }
    }
}
