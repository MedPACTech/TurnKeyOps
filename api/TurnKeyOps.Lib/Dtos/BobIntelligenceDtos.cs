using System.Text.Json;
using System.Text.Json.Serialization;
namespace TurnKeyOps.Lib.Dtos;

[JsonConverter(typeof(JsonNumberEnumConverter<BobAutonomy>))]
public enum BobAutonomy { DISABLED, RECOMMEND_ONLY, DRAFT, ACT_WITH_APPROVAL, AUTO_ACT_BY_POLICY }
[JsonConverter(typeof(JsonNumberEnumConverter<BobRiskLevel>))]
public enum BobRiskLevel { LOW, MEDIUM, HIGH, EXPLICIT_HUMAN }
[JsonConverter(typeof(JsonNumberEnumConverter<AttentionSeverity>))]
public enum AttentionSeverity { INFO, ACTION, WARNING, CRITICAL }

public sealed class BobIntelligenceState
{
    public string Version { get; set; } = "";
    public BobTenantPolicy Policy { get; set; } = new();
    public Dictionary<string, Guid> ExecutionClaims { get; set; } = [];
    public List<BobAttentionItem> Attention { get; set; } = [];
    public List<BobWorkflow> Workflows { get; set; } = [];
    public List<BobAutomationRule> Rules { get; set; } = [];
    public List<BobFeedback> Feedback { get; set; } = [];
    public List<BobOperationalEvent> Events { get; set; } = [];
}
public sealed class BobTenantPolicy
{
    public bool Enabled { get; set; } = true;
    public bool ProactiveEnabled { get; set; } = true;
    // An explicit allow-list overrides legacy availability; absence preserves legacy operational modules.
    public string[] EnabledModules { get; set; } = ["leads", "estimates", "jobs", "calendar", "inventory", "purchasing", "finance", "payroll", "bob"];
    public Dictionary<string, BobAutonomy> Actions { get; set; } = [];
    public List<BobEscalationRule> Escalations { get; set; } = [];
    public Dictionary<string, string[]> RoleModules { get; set; } = [];
    public bool ExternalNotificationsEnabled { get; set; }
    public bool CustomerBobEnabled { get; set; }
}
public sealed record BobEscalationRule(string Module, string Condition, int AfterHours, string Team, AttentionSeverity Severity);
public sealed record BobSourceReference(string Module, string EntityId, string Condition, string Version = "");
public sealed class BobAttentionItem
{
    public string Id { get; set; } = "";
    public Guid ActorUserId { get; set; }
    public BobSourceReference Source { get; set; } = new("", "", "");
    public AttentionSeverity Severity { get; set; }
    public string Title { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Reason { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public string? CapabilityId { get; set; }
    public DateTime FirstObservedUtc { get; set; }
    public DateTime LastObservedUtc { get; set; }
    public DateTime? DueUtc { get; set; }
    public string Status { get; set; } = "open";
    public string? EscalatedTo { get; set; }
    public DateTime? ResolvedUtc { get; set; }
    public string? ResolutionSource { get; set; }
}
public sealed record BobContextFact(BobSourceReference Source, string Title, string Explanation, string NextAction,
    AttentionSeverity Severity, DateTime? DueUtc = null, string? CapabilityId = null, string Trade = "general");
public sealed record BobOperatingContext(Guid TenantId, Guid ActorUserId, string Role, DateTime AsOfUtc,
    string[] Modules, IReadOnlyList<BobContextFact> Facts, string[] UnavailableModules, string[]? IncompleteModules = null);
public sealed record BobNextAction(string Action, string Rationale, BobSourceReference Source, string? CapabilityId,
    bool Eligible, string[] BlockingRequirements, BobRiskLevel Risk, BobAutonomy Policy, bool CanExecute);
public sealed record BobBriefing(BobOperatingContext Context, IReadOnlyList<BobAttentionItem> Priorities,
    IReadOnlyList<BobAttentionItem> AtRisk, IReadOnlyList<BobWorkflow> NeedsDecision,
    IReadOnlyList<BobWorkflow> Handled, IReadOnlyList<BobWorkflow> Watching, IReadOnlyList<BobNextAction> NextActions);

public sealed class BobWorkflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorUserId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string InputFingerprint { get; set; } = "";
    public string Goal { get; set; } = "";
    public string Status { get; set; } = "ready";
    public List<BobWorkflowStep> Steps { get; set; } = [];
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? Failure { get; set; }
}
public sealed class BobWorkflowStep
{
    public string CapabilityId { get; set; } = "";
    public Guid ConversationId { get; set; }
    public JsonElement Input { get; set; } = JsonSerializer.SerializeToElement(new { });
    public string Rationale { get; set; } = "";
    public Guid? ActionId { get; set; }
    public string Status { get; set; } = "pending";
    public string? Failure { get; set; }
    public BobSourceReference? Watching { get; set; }
}
public sealed class BobAutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public string Module { get; set; } = "";
    public string Condition { get; set; } = "";
    public AttentionSeverity MinimumSeverity { get; set; } = AttentionSeverity.ACTION;
    public string Action { get; set; } = "attention";
    public DateTime? EffectiveUtc { get; set; }
}
public sealed record BobOperationalEvent(Guid Id, Guid ActorUserId, DateTime AtUtc, string Action, string TargetId, string Rationale, string Module = "bob");
public sealed record BobFeedback(Guid Id, Guid ActorUserId, string TargetId, string Kind, DateTime AtUtc);
public sealed record BobCommandRequest(string Text, string? Module = null, string? EntityId = null);
public sealed record BobCommandResult(string Intent, string Status, string Message, IReadOnlyList<BobContextFact> Matches, IReadOnlyList<BobNextAction> Actions);
public sealed record BobPolicyUpdate(string ExpectedVersion, BobTenantPolicy Policy);
public sealed record BobRuleUpdate(string ExpectedVersion, BobAutomationRule Rule);
public sealed record BobWorkflowRequest(string Goal, string IdempotencyKey, List<BobWorkflowStep> Steps);
public sealed record BobFeedbackRequest(string TargetId, string Kind);
