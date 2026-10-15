using System.Text.Json;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;

public sealed partial class BobIntelligenceService(IBobIntelligenceStore store, BobCapabilityRegistry registry,
    IEnumerable<IBobContextSource> sources, IEnumerable<IBobActionProvider> providers, IBobActionGuard guard,
    IBobOperationsService operations, UserModuleAccessService grants, IRoleAccessService roles,
    ITenantMembershipRepository memberships, IChatRepository chats, IUserContext user,
    IOptions<BobOperationsOptions> options, IPayrollStore payroll, IAuditService audit)
{
    private readonly Dictionary<string, IBobActionProvider> _providers = providers.ToDictionary(p => p.ToolKey, StringComparer.Ordinal);
    private async Task<(BobIntelligenceState State, string[] Permissions, string Role)> Access(CancellationToken ct, bool allowDisabled = false)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty || user.UserId == Guid.Empty) throw new UnauthorizedAccessException();
        var permissions = await grants.GetAsync(ct);
        if (!permissions.Contains("bob.read")) throw new MedInsights.Lib.ForbiddenAccessException("Bob read access is required.");
        var state = await store.ReadAsync(user.TenantId, ct);
        if (!allowDisabled && (!options.Value.Enabled || !state.Policy.Enabled || !state.Policy.EnabledModules.Contains("bob"))) throw new InvalidOperationException("Bob is disabled.");
        var member = await memberships.GetByUserIdAsync(MedInsights.Lib.EntityKeyPolicy.TenantPartition(user.TenantId), user.UserId, ct);
        if (!MedInsights.Lib.Authorization.UserModulePermissions.IsActive(member)) throw new UnauthorizedAccessException();
        return (state, permissions, member!.Role);
    }
    private async Task<HashSet<string>> Readable(BobTenantPolicy policy, string[] permissions, CancellationToken ct)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in policy.EnabledModules.Where(m => m != "bob" && permissions.Contains(m + ".read")))
            if (module is "finance" or "payroll" || await roles.HasPermissionAsync(module + ".read", ct)) allowed.Add(module);
        // Payroll's own module availability is authoritative and independently disabled by default.
        if (allowed.Contains("payroll") && !(await payroll.ReadAsync(user.TenantId, ct)).Configuration.Enabled) allowed.Remove("payroll");
        return allowed;
    }
    public async Task<BobOperatingContext> ContextAsync(string? module = null, string? entityId = null, CancellationToken ct = default)
    {
        var (state, permissions, role) = await Access(ct);
        var allowed = await Readable(state.Policy, permissions, ct);
        if (module is not null && !allowed.Contains(module)) throw new MedInsights.Lib.ForbiddenAccessException("Target module is unavailable or unauthorized.");
        if (entityId is not null && (entityId.Length > 100 || module is null)) throw new ArgumentException("Focused context requires a module and a valid entity reference.");
        var requested = module is null ? allowed : new HashSet<string> { module };
        if (module is null && state.Policy.RoleModules.TryGetValue(role, out var selected)) requested.IntersectWith(selected);
        var now = DateTime.UtcNow; var facts = new List<BobContextFact>(); var unavailable = new List<string>(); var incomplete = new List<string>();
        foreach (var m in requested.Order())
        {
            var source = sources.SingleOrDefault(s => s.Modules.Contains(m));
            if (source is null) { unavailable.Add(m); continue; }
            try { var result = await source.ReadAsync(m, entityId, now, ct); facts.AddRange(result.Facts); if (!result.Complete) incomplete.Add(m); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { unavailable.Add(m); } // No stale or invented facts after a module failure.
        }
        return new(user.TenantId, user.UserId, role, now, requested.ToArray(), facts.Take(480).ToArray(), unavailable.ToArray(), incomplete.ToArray());
    }
    public async Task<IReadOnlyList<object>> CapabilitiesAsync(CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct); var readable = await Readable(state.Policy, permissions, ct);
        var result = new List<object>();
        foreach (var c in registry.All.Where(c => c.ModuleDependencies.All(m => m == "bob" || readable.Contains(m))))
        {
            var canExecute = false; string? blocker = null; bool approval = true;
            try { approval = await guard.RequireAsync(_providers[c.Id], ct); canExecute = c.WritePermissions.Length == 0 || options.Value.WriteActionsEnabled; }
            catch (Exception e) when (e is MedInsights.Lib.ForbiddenAccessException or InvalidOperationException) { blocker = e.Message; }
            result.Add(new { Capability = c, Policy = state.Policy.Actions.GetValueOrDefault(c.Id,
                c.WritePermissions.Length == 0 ? BobAutonomy.AUTO_ACT_BY_POLICY : BobAutonomy.ACT_WITH_APPROVAL), CanExecute = canExecute, RequiresApproval = approval, Blocker = blocker });
        }
        return result;
    }
    public async Task<BobNextAction> NextAsync(BobContextFact fact, CancellationToken ct)
    {
        var (state, _, _) = await Access(ct);
        if (fact.CapabilityId is null) return new(fact.NextAction, fact.Explanation, fact.Source, null, false, ["Review in the source module"], BobRiskLevel.LOW, BobAutonomy.RECOMMEND_ONLY, false);
        var c = registry.Get(fact.CapabilityId); var blockers = new List<string>(); var approval = true;
        var mode = state.Policy.Actions.GetValueOrDefault(c.Id, c.WritePermissions.Length == 0 ? BobAutonomy.AUTO_ACT_BY_POLICY : BobAutonomy.ACT_WITH_APPROVAL);
        try { approval = await guard.RequireAsync(_providers[c.Id], ct); }
        catch (Exception e) when (e is MedInsights.Lib.ForbiddenAccessException or InvalidOperationException) { blockers.Add(e.Message); }
        if (c.WritePermissions.Length > 0 && !options.Value.WriteActionsEnabled) blockers.Add("Bob write actions are disabled");
        // Recommendations never imply the missing business inputs have been supplied or validated.
        if (c.RequiredInputs.Length > 0) blockers.Add("Review current source record and required action inputs");
        return new(fact.NextAction, fact.Explanation, fact.Source, c.Id, blockers.Count == 0, blockers.ToArray(), c.Risk, mode, blockers.Count == 0 && !approval);
    }
    public async Task<BobBriefing> BriefingAsync(CancellationToken ct = default)
    {
        var context = await ContextAsync(ct: ct); var (state, _, _) = await Access(ct);
        if (state.Policy.ProactiveEnabled)
        {
            var before = JsonSerializer.Serialize(new { Attention = state.Attention.Select(a => new { a.Id, a.Status, a.Severity, a.Explanation, a.EscalatedTo }), Workflows = state.Workflows.Select(w => new { w.Id, w.Status }), Events = state.Events.Count });
            var conditionEvents = BobAutomationEngine.Events(state, context);
            BobAutomationEngine.Handle(state, context, conditionEvents);
            BobAttentionEngine.Reconcile(state, context);
            foreach (var watching in state.Workflows.Where(w => w.ActorUserId == user.UserId && w.Status == "watching"))
            {
                var reference = watching.Steps.FirstOrDefault()?.Watching;
                if (reference is null || !context.Modules.Contains(reference.Module) || context.UnavailableModules.Contains(reference.Module) || (context.IncompleteModules?.Contains(reference.Module) ?? false)) continue;
                if (!context.Facts.Any(f => f.Source.Module == reference.Module && f.Source.EntityId == reference.EntityId && f.Source.Condition == reference.Condition))
                {
                    watching.Status = "completed"; watching.UpdatedUtc = context.AsOfUtc;
                    watching.Steps[0].Status = "completed";
                    state.Events.Add(new(Guid.NewGuid(), user.UserId, context.AsOfUtc, "watch.condition_resolved", watching.Id.ToString(), "The watched condition was absent from a complete authorized source check", reference.Module));
                }
            }
            var after = JsonSerializer.Serialize(new { Attention = state.Attention.Select(a => new { a.Id, a.Status, a.Severity, a.Explanation, a.EscalatedTo }), Workflows = state.Workflows.Select(w => new { w.Id, w.Status }), Events = state.Events.Count });
            if (before != after) await Save(state, "attention.updated", "briefing", "An authorized source condition changed", ct);
            else await store.SaveAsync(user.TenantId, state, state.Version, ct);
        }
        var attention = state.Attention.Where(a => a.ActorUserId == user.UserId && a.Status == "open" && context.Modules.Contains(a.Source.Module) && !context.UnavailableModules.Contains(a.Source.Module))
            .OrderByDescending(a => a.Severity).ThenBy(a => BobAttentionEngine.RolePriority(context.Role, a.Source.Module)).ThenBy(a => a.DueUtc ?? DateTime.MaxValue).ToArray();
        var workflows = await VisibleWorkflows(state, ct);
        var next = new List<BobNextAction>();
        foreach (var fact in context.Facts.OrderByDescending(f => f.Severity).ThenBy(f => BobAttentionEngine.RolePriority(context.Role, f.Source.Module)).Take(8)) next.Add(await NextAsync(fact, ct));
        return new(context, attention.Take(5).ToArray(), attention.Where(a => a.Severity >= AttentionSeverity.WARNING).Take(8).ToArray(),
            workflows.Where(w => w.Status == "waiting_approval").ToArray(), workflows.Where(w => w.Status == "completed").TakeLast(10).ToArray(), workflows.Where(w => w.Status == "watching").ToArray(), next);
    }
    private async Task<BobWorkflow[]> VisibleWorkflows(BobIntelligenceState state, CancellationToken ct)
    {
        var permissions = await grants.GetAsync(ct); var readable = await Readable(state.Policy, permissions, ct);
        return state.Workflows.Where(w => w.ActorUserId == user.UserId && w.Steps.All(s => registry.Get(s.CapabilityId).ModuleDependencies.All(m => m == "bob" || readable.Contains(m)) && (s.Watching is null || readable.Contains(s.Watching.Module)))).Select(PublicWorkflow).ToArray();
    }
    private static BobWorkflow PublicWorkflow(BobWorkflow workflow)
    {
        var safe = JobConfigurationService.Clone(workflow);
        // Input history is not a context source. Only the fields needed for a current confirmation are visible.
        var visible = new HashSet<string>(["leadId", "jobId", "estimateId", "subject", "body", "text", "startUtc", "endUtc", "state", "action", "id", "targetId"], StringComparer.Ordinal);
        foreach (var step in safe.Steps)
        {
            var fields = step.Status == "waiting_approval" && step.Input.ValueKind == JsonValueKind.Object
                ? step.Input.EnumerateObject().Where(p => visible.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value.Clone())
                : new Dictionary<string, JsonElement>();
            step.Input = JsonSerializer.SerializeToElement(fields);
        }
        return safe;
    }
    public async Task<object> ActivityAsync(CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct); var readable = await Readable(state.Policy, permissions, ct);
        var workflows = await VisibleWorkflows(state, ct);
        // Inputs contain internal source text and are never returned in Activity.
        return new { Workflows = workflows.Select(w => new { w.Id, w.Goal, w.Status, w.CreatedUtc, w.UpdatedUtc, w.Failure,
            Steps = w.Steps.Select(s => new { s.CapabilityId, s.Status, s.Rationale, s.ActionId, s.Failure, s.Watching }) }),
            Events = state.Events.Where(e => e.ActorUserId == user.UserId && (e.Module == "bob" || readable.Contains(e.Module))).TakeLast(100),
            Metrics = new { Completed = workflows.Count(w => w.Status == "completed"), WaitingApproval = workflows.Count(w => w.Status == "waiting_approval"),
                Failed = workflows.Count(w => w.Status == "needs_review"), AttentionResolved = state.Attention.Count(a => a.ActorUserId == user.UserId && readable.Contains(a.Source.Module) && a.Status == "resolved"),
                Feedback = state.Feedback.Count(f => f.ActorUserId == user.UserId) } };
    }
    public async Task<BobCommandResult> CommandAsync(BobCommandRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000) throw new ArgumentException("Enter a command of 1–2000 characters.");
        // Language is a retrieval hint only. It cannot select an executable command or override authority.
        var text = request.Text.ToLowerInvariant();
        if (text.Contains("ignore") && (text.Contains("permission") || text.Contains("policy")) || text.Contains("cross-tenant"))
            return new("unsupported", "denied", "Permissions and tenant policy always apply.", [], []);
        var module = request.Module ?? (text.Contains("payroll") || text.Contains("time entr") ? "payroll" : text.Contains("invoice") || text.Contains("cash") || text.Contains("journal") ? "finance" : text.Contains("estimate") || text.Contains("proposal") ? "estimates" : text.Contains("lead") ? "leads" : text.Contains("material") || text.Contains("stock") ? "inventory" : text.Contains("vendor") || text.Contains("purchase") ? "purchasing" : text.Contains("schedule") || text.Contains("appointment") ? "calendar" : text.Contains("job") ? "jobs" : null);
        var context = await ContextAsync(module, request.EntityId, ct);
        var matches = context.Facts.OrderByDescending(f => f.Severity).ToArray();
        if (request.EntityId is null && module is not null && matches.Select(f => f.Source.EntityId).Distinct().Count() > 1 &&
            new[] { "handle", "send", "assign", "prepare", "schedule", "post", "change" }.Any(text.Contains))
            return new("entity-resolution", "clarification_required", "Choose the source record before continuing.", matches.Take(10).ToArray(), []);
        var actions = new List<BobNextAction>(); foreach (var f in matches.Take(5)) actions.Add(await NextAsync(f, ct));
        return new("review", context.UnavailableModules.Length > 0 ? "partial" : "ready", matches.Length == 0 ? "No matching current conditions were found. No business data changed." : "Review these source facts and next steps. No business data changed.", matches.Take(10).ToArray(), actions);
    }
    public async Task<object> AutomationCenterAsync(CancellationToken ct = default)
    {
        var (state, _, _) = await Access(ct, allowDisabled: true); await RequireAdmin(ct);
        return new { state.Version, state.Policy, state.Rules, Capabilities = registry.All,
            RecentActivity = state.Events.Where(e => e.ActorUserId == user.UserId).TakeLast(50),
            Suggestions = new[] { new { Id = "attention-review", Title = "Review recurring attention conditions", Description = "Add an attention rule after reviewing the source condition. Suggestions never enable themselves." } } };
    }
    private async Task RequireAdmin(CancellationToken ct)
    { if (!await grants.IsOwnerAsync(ct) || !(await grants.GetAsync(ct)).Contains("bob.write")) throw new MedInsights.Lib.ForbiddenAccessException("A tenant owner with Bob write access must configure automation."); }
    public async Task UpdatePolicyAsync(BobPolicyUpdate update, CancellationToken ct = default)
    {
        var (state, _, _) = await Access(ct, allowDisabled: true); await RequireAdmin(ct);
        if (update.ExpectedVersion != state.Version) throw new InvalidOperationException("Policy changed. Refresh first.");
        ValidatePolicy(update.Policy); state.Policy = update.Policy;
        await Save(state, "policy.updated", "policy", "Owner explicitly updated Bob authority", ct);
    }
    public void ValidatePolicy(BobTenantPolicy policy)
    {
        var known = registry.All.Select(c => c.Module).Append("calendar").ToHashSet();
        if (policy.EnabledModules.Any(m => !known.Contains(m)) || policy.Actions.Count > registry.All.Count || policy.Escalations.Count > 100 || policy.RoleModules.Count > 30) throw new ArgumentException("Unsupported Bob policy limits or module.");
        foreach (var p in policy.Actions) { registry.Get(p.Key); if (!Enum.IsDefined(p.Value)) throw new ArgumentException("Invalid action policy."); }
        if (policy.Escalations.Any(r => !known.Contains(r.Module) || r.AfterHours is < 0 or > 8760 || r.Team.Length is < 1 or > 100 || r.Condition.Length is < 1 or > 100 || !Enum.IsDefined(r.Severity))) throw new ArgumentException("Invalid escalation rule.");
        if (policy.CustomerBobEnabled) throw new ArgumentException("Customer intelligence is not enabled through the internal context service. Use the entitlement-scoped portal.");
    }
    public async Task UpdateRuleAsync(BobRuleUpdate update, CancellationToken ct = default)
    {
        var (state, _, _) = await Access(ct, allowDisabled: true); await RequireAdmin(ct);
        if (state.Version != update.ExpectedVersion) throw new InvalidOperationException("Automation changed. Refresh first.");
        var r = update.Rule;
        if (r.Id == Guid.Empty || r.Name.Length is < 1 or > 120 || r.Condition.Length is < 1 or > 100 || r.Action != "attention" || !Enum.IsDefined(r.MinimumSeverity) || !state.Policy.EnabledModules.Contains(r.Module) || state.Rules.Count >= 100 && state.Rules.All(x => x.Id != r.Id)) throw new ArgumentException("V1 rules require an enabled source module, a canonical condition, and the attention action.");
        r.OwnerUserId = user.UserId; state.Rules.RemoveAll(x => x.Id == r.Id); state.Rules.Add(r);
        await Save(state, "rule.updated", r.Id.ToString(), "Owner explicitly configured a structured attention rule", ct);
    }
    public async Task DismissAsync(string id, CancellationToken ct = default)
    {
        var (state, permissions, _) = await Access(ct); var allowed = await Readable(state.Policy, permissions, ct);
        var item = state.Attention.SingleOrDefault(a => a.Id == id && a.ActorUserId == user.UserId && allowed.Contains(a.Source.Module)) ?? throw new KeyNotFoundException();
        item.Status = "dismissed"; item.ResolutionSource = "user dismissed";
        await Save(state, "attention.dismissed", id, "User dismissed the current condition", ct, item.Source.Module);
    }
    public async Task FeedbackAsync(BobFeedbackRequest request, CancellationToken ct = default)
    {
        var (state, _, _) = await Access(ct);
        if (request.Kind is not ("helpful" or "not-helpful" or "wrong-recommendation" or "wrong-data" or "should-not-run") || request.TargetId.Length is < 1 or > 128) throw new ArgumentException("Choose a supported feedback reason.");
        var workflows = await VisibleWorkflows(state, ct);
        var permissions = await grants.GetAsync(ct); var allowed = await Readable(state.Policy, permissions, ct);
        if (!workflows.Any(w => w.Id.ToString() == request.TargetId) && !state.Attention.Any(a => a.Id == request.TargetId && a.ActorUserId == user.UserId && allowed.Contains(a.Source.Module))) throw new KeyNotFoundException("Feedback target is unavailable.");
        state.Feedback.Add(new(Guid.NewGuid(), user.UserId, request.TargetId, request.Kind, DateTime.UtcNow));
        await Save(state, "feedback.recorded", request.TargetId, request.Kind, ct);
    }
    private async Task Save(BobIntelligenceState state, string action, string target, string rationale, CancellationToken ct, string module = "bob")
    {
        state.Events.Add(new(Guid.NewGuid(), user.UserId, DateTime.UtcNow, action, target, rationale, module));
        await store.SaveAsync(user.TenantId, state, state.Version, ct);
        await audit.RecordAsync(new MedInsights.Lib.Dtos.RecordAuditEventRequestDto { Category = "bob_intelligence", Action = action, TargetType = "bob", TargetId = target, Source = "bob_intelligence", Description = rationale }, ct);
    }
}
