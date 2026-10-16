using MedInsights.Lib.Entities;

namespace MedInsights.Lib.Authorization;

public static class UserModulePermissions
{
    public static readonly string[] Modules = ["dashboard", "bob", "calendar", "jobs", "leads", "requests", "estimates", "invoices", "contacts", "users", "settings", "billing", "inventory", "purchasing", "finance", "payroll"];
    public static readonly string[] PayrollActions = ["time.self", "time.crew", "time.approve", "payroll.prepare", "payroll.export", "payroll.results", "payroll.compensation.read", "payroll.admin"];
    public static readonly string[] EstimateActions = ["estimates.submit", "estimates.manage"];
    public static readonly string[] Operations = ["calendar", "jobs", "leads", "requests", "estimates", "invoices", "contacts"];
    public static bool IsActive(TenantMembership? membership) => membership is not null && !membership.IsDeleted
        && membership.DateRemoved is null && string.Equals(membership.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase);
    public static string[] Resolve(TenantMembership? membership, UserProfile? profile, EmployeeAccessConfiguration? configuration = null)
    {
        if (!IsActive(membership)) return [];
        if (membership!.IsOwner || membership.Role.Equals("owner", StringComparison.OrdinalIgnoreCase))
            return Modules.SelectMany(m => new[] { m + ".read", m + ".write" }).Concat(PayrollActions).Concat(EstimateActions).ToArray();
        if (profile is not null && (!profile.IsActive || profile.IsDeleted)) return [];
        var role = membership.Role.ToLowerInvariant();
        var defaults = role switch {
            "admin" => Modules,
            "staff" or "member" => Modules.Except(["users", "settings", "billing"]).ToArray(),
            "billing_admin" => new[] { "billing" },
            _ => Array.Empty<string>()
        };
        var assignedRole = profile?.EmployeeRoleId is { Length: > 0 } roleId ? (configuration ?? EmployeeAccessConfiguration.Default()).Roles.FirstOrDefault(r => r.Id == roleId) : null;
        if (!string.IsNullOrEmpty(profile?.EmployeeRoleId) && assignedRole is null) return [];
        var allowed = defaults.SelectMany(m => new[] { m + ".read", m + ".write" }).Concat(PayrollActions.Where(p => role=="admin" || p is not ("payroll.admin" or "payroll.compensation.read"))).ToArray();
        if (assignedRole is null && profile?.ModulePermissions is null && role is not ("owner" or "admin")) allowed = allowed.Where(p => !p.StartsWith("inventory.") && !p.StartsWith("purchasing.") && !p.StartsWith("finance.") && !p.StartsWith("payroll.") && !p.StartsWith("time.")).ToArray();
        if(profile?.ModulePermissions is null)allowed=allowed.Where(p=>p is not("payroll.admin" or "payroll.compensation.read")).ToArray();
        if (allowed.Contains("estimates.write")) allowed = allowed.Concat(EstimateActions).ToArray();
        if (assignedRole is not null) allowed = allowed.Intersect(assignedRole.ModulePermissions, StringComparer.Ordinal).ToArray();
        if (profile?.ModulePermissions is null) return allowed;
        var overrides = profile.ModulePermissions.AsEnumerable();
        // Preserve the meaning of existing write overrides before employee roles were introduced.
        if (assignedRole is null && profile.ModulePermissions.Contains("estimates.write")) overrides = overrides.Concat(EstimateActions);
        return allowed.Intersect(overrides, StringComparer.Ordinal).ToArray();
    }
    public static bool Allows(IEnumerable<string> permissions, string module, bool write)
    {
        var set = permissions.ToHashSet(StringComparer.Ordinal);
        var action = write ? ".write" : ".read";
        if (!set.Contains(module + ".read") || (write && !set.Contains(module + action))) return false;
        // Dashboard aggregates operational data. Bob scopes context and actions per target module.
        if (module == "dashboard")
            return Operations.All(m => set.Contains(m + ".read"));
        return true;
    }
    public static string[]? Validate(string[]? permissions)
    {
        if (permissions is null) return null;
        var known = Modules.SelectMany(m => new[] { m + ".read", m + ".write" }).Concat(PayrollActions).Concat(EstimateActions).ToHashSet();
        if (permissions.Any(p => !known.Contains(p))) throw new ArgumentException("Unknown module permission.");
        if (permissions.Any(p => p.EndsWith(".write") && !permissions.Contains(p.Replace(".write", ".read"))))
            throw new ArgumentException("Write access also requires read access.");
        return permissions.Distinct().Order().ToArray();
    }
}
