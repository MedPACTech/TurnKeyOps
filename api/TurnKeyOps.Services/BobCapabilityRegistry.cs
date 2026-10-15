using System.Text.Json;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Services.Interfaces;
namespace TurnKeyOps.Services;

public sealed record BobCapability(string Id, string Module, string[] ReadPermissions, string[] WritePermissions,
    string[] ModuleDependencies, BobRiskLevel Risk, bool IsDraft, string[] RequiredInputs, string ExecutedService,
    string Rationale, bool RequiresIdempotency = true)
{
    public void Validate(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) throw new ArgumentException("Action input must be an object.");
        if (input.GetRawText().Length > 8000) throw new ArgumentException("Action input exceeds 8000 characters.");
        foreach (var key in RequiredInputs)
        {
            if (!input.TryGetProperty(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
                throw new ArgumentException($"Action requires {key}.");
            if (key.EndsWith("Id") && (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var id) || id == Guid.Empty))
                throw new ArgumentException($"Action requires a valid {key}.");
        }
    }
}
public sealed class BobCapabilityRegistry
{
    private readonly Dictionary<string, BobCapability> _capabilities;
    public BobCapabilityRegistry(IEnumerable<IBobActionProvider> providers)
    {
        _capabilities = providers.ToDictionary(p => p.ToolKey, Describe, StringComparer.Ordinal);
    }
    public IReadOnlyCollection<BobCapability> All => _capabilities.Values;
    public BobCapability Get(string id) => _capabilities.GetValueOrDefault(id) ?? throw new ArgumentException("Unknown Bob capability.");
    private static BobCapability Describe(IBobActionProvider p)
    {
        var module = p.ToolKey.Split('.')[0] switch { "lead" => "leads", "estimate" => "estimates", "job" => "jobs", "supply" => p.PermissionKey.Split('.')[0], "conversation" => "bob", var m => m };
        var write = p.Risk != BobActionRisk.Read;
        var draft = p.ToolKey is "lead.draft" or "estimate.extract" or "job.change" or "supply.draft-order" or "finance.draft-journal";
        var high = p.Risk is BobActionRisk.Financial or BobActionRisk.CustomerFacing or BobActionRisk.Scheduling || p.ToolKey is "lead.stage" or "job.transition" or "lead.send" or "lead.schedule" or "estimate.issue" or "estimate.remind" or "job.notify" or "job.schedule" or "supply.send" or "payroll.prepare";
        var risk = p.Risk == BobActionRisk.Read || draft ? BobRiskLevel.LOW : high ? BobRiskLevel.HIGH : p.ToolKey == "conversation.archive" ? BobRiskLevel.EXPLICIT_HUMAN : BobRiskLevel.MEDIUM;
        var target = module switch { "leads" => "leadId", "jobs" => "jobId", "estimates" => "estimateId", _ => "" };
        var inputs = new List<string>();
        if (target != "") inputs.Add(target);
        if (write && module is "leads" or "jobs" or "estimates") inputs.Add("expectedVersion");
        var reads = new List<string> { module + ".read" };
        var writes = write ? new List<string> { module + ".write" } : [];
        var deps = new List<string> { module };
        if (p.ToolKey is "lead.schedule" or "job.schedule" or "job.recommend") { reads.Add("calendar.read"); deps.Add("calendar"); if (write) writes.Add("calendar.write"); }
        return new(p.ToolKey, module, reads.ToArray(), writes.ToArray(), deps.ToArray(), risk, draft, inputs.ToArray(),
            p.GetType().Name, "Module validation and current tenant action policy apply; source values remain authoritative.");
    }
}
public static class BobPolicyRules
{
    public static BobAutonomy Normalize(string mode) => mode.ToLowerInvariant() switch {
        "disabled" => BobAutonomy.DISABLED, "recommend" or "recommend_only" => BobAutonomy.RECOMMEND_ONLY,
        "draft" => BobAutonomy.DRAFT, "approval" or "act_with_approval" => BobAutonomy.ACT_WITH_APPROVAL,
        "auto" or "read" or "auto_act_by_policy" => BobAutonomy.AUTO_ACT_BY_POLICY,
        _ => throw new ArgumentException("Unknown Bob action policy.")
    };
    public static bool RequiresApproval(BobCapability c, BobAutonomy mode)
    {
        if (!Enum.IsDefined(mode) || mode is BobAutonomy.DISABLED or BobAutonomy.RECOMMEND_ONLY)
            throw new InvalidOperationException("Tenant policy allows no execution for this capability.");
        if (mode == BobAutonomy.DRAFT && !c.IsDraft && c.WritePermissions.Length > 0)
            throw new InvalidOperationException("Draft authority cannot perform this action.");
        // V1 has no outcome guardrail for external or restricted actions. Tenant auto cannot lower this floor.
        return c.Risk is BobRiskLevel.HIGH or BobRiskLevel.EXPLICIT_HUMAN || mode == BobAutonomy.ACT_WITH_APPROVAL;
    }
}
