using MedInsights.Lib.Authorization;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using System.Text.Json;
namespace TurnKeyOps.Services;

public interface IBobActionGuard
{
    Task<bool> RequireAsync(IBobActionProvider provider, CancellationToken ct);
    Task ClaimExecutionAsync(string idempotencyKey, Guid actionId, CancellationToken ct);
}
public sealed class BobActionGuard(BobCapabilityRegistry registry, UserModuleAccessService grants,
    IRoleAccessService roles, IUserContext user, IBobIntelligenceStore store, ITenantSettingsRepository settings,
    IPayrollStore payroll, ISupplyStore supply) : IBobActionGuard
{
    public async Task ClaimExecutionAsync(string idempotencyKey, Guid actionId, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.UserId == Guid.Empty || user.TenantId == Guid.Empty || actionId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey)) throw new UnauthorizedAccessException();
        var state = await store.ReadAsync(user.TenantId, ct);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{user.UserId:N}|{idempotencyKey}")));
        if (state.ExecutionClaims.ContainsKey(key)) throw new InvalidOperationException("This write has already claimed execution. Reconcile its source result before attempting another action.");
        state.ExecutionClaims[key] = actionId;
        // A conditional tenant commit makes direct action races safe across API instances as well as workflows.
        await store.SaveAsync(user.TenantId, state, state.Version, ct);
    }
    public async Task<bool> RequireAsync(IBobActionProvider provider, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty || user.UserId == Guid.Empty) throw new UnauthorizedAccessException();
        var capability = registry.Get(provider.ToolKey);
        var policy = (await store.ReadAsync(user.TenantId, ct)).Policy;
        var permissions = await grants.GetAsync(ct);
        if (!policy.Enabled || !policy.EnabledModules.Contains("bob") || capability.ModuleDependencies.Any(m => !policy.EnabledModules.Contains(m)))
            throw new InvalidOperationException("Bob or a required module is disabled for this tenant.");
        if (!policy.ExternalNotificationsEnabled && provider.ToolKey is "lead.send" or "estimate.remind" or "job.notify" or "supply.send")
            throw new InvalidOperationException("Tenant policy has not enabled external Bob communication.");
        var required = capability.ReadPermissions.Concat(capability.WritePermissions).Append("bob.read")
            .Concat(capability.WritePermissions.Length > 0 ? new[] { "bob.write" } : []);
        if (required.Any(p => !permissions.Contains(p))) throw new MedInsights.Lib.ForbiddenAccessException("Bob requires both Bob and target module grants.");
        await roles.RequirePermissionAsync(provider.PermissionKey, ct);
        var mode = capability.WritePermissions.Length == 0 || capability.IsDraft ? BobAutonomy.AUTO_ACT_BY_POLICY : BobAutonomy.ACT_WITH_APPROVAL;
        // Existing module policy remains a ceiling; central policy cannot reopen a module-disabled action.
        using var doc = JsonDocument.Parse((await settings.GetAsync(RepositoryKeyHelper.ToTenantPartitionKey(user.TenantId), "SETTINGS|OPERATIONAL", ct))?.ValuesJson ?? "{}");
        var legacyModule = capability.Module == "leads" || capability.Module == "estimates" ? "leads" : capability.Module;
        var legacyApproval = false;
        if (doc.RootElement.TryGetProperty(legacyModule, out var module) && module.TryGetProperty("aiActions", out var actions) && actions.TryGetProperty(provider.ToolKey, out var legacy))
        {
            var value = legacy.GetString() ?? "disabled";
            if (value == "read" && capability.WritePermissions.Length > 0) throw new InvalidOperationException("Module read-only policy cannot execute a mutation.");
            mode = BobPolicyRules.Normalize(value);
            legacyApproval = mode == BobAutonomy.ACT_WITH_APPROVAL;
        }
        if (capability.Module is "inventory" or "purchasing")
        {
            var state = await supply.ReadAsync(user.TenantId, ct);
            if (state.Policy.AiActions.TryGetValue(provider.ToolKey, out var value)) {
                if (value == "read" && capability.WritePermissions.Length > 0) throw new InvalidOperationException("Module read-only policy cannot execute a mutation.");
                mode = BobPolicyRules.Normalize(value); legacyApproval = mode == BobAutonomy.ACT_WITH_APPROVAL;
            }
        }
        if (capability.Module == "payroll")
        {
            var state = await payroll.ReadAsync(user.TenantId, ct);
            if (!state.Configuration.Enabled) throw new InvalidOperationException("Payroll is disabled.");
            var value = state.Configuration.AiActions.GetValueOrDefault(provider.ToolKey, "disabled");
            if (value == "read" && capability.WritePermissions.Length > 0) throw new InvalidOperationException("Module read-only policy cannot execute a mutation.");
            mode = BobPolicyRules.Normalize(value); legacyApproval = mode == BobAutonomy.ACT_WITH_APPROVAL;
        }
        if (mode == BobAutonomy.DRAFT && capability.WritePermissions.Length > 0 && !capability.IsDraft)
            throw new InvalidOperationException("Module draft policy cannot execute this action.");
        if (mode is BobAutonomy.DISABLED or BobAutonomy.RECOMMEND_ONLY) return BobPolicyRules.RequiresApproval(capability, mode);
        if (policy.Actions.TryGetValue(provider.ToolKey, out var configured)) mode = configured;
        return BobPolicyRules.RequiresApproval(capability, mode) || legacyApproval;
    }
}
