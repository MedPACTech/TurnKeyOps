using MedInsights.Lib.Entities;

namespace MedInsights.Lib.Authorization;

public static class UserModulePermissions
{
    public static readonly string[] Modules = ["dashboard", "bob", "calendar", "jobs", "requests", "estimates", "invoices", "contacts", "users", "settings", "billing"];
    public static readonly string[] Operations = ["calendar", "jobs", "requests", "estimates", "invoices", "contacts"];
    public static bool IsActive(TenantMembership? membership) => membership is not null && !membership.IsDeleted
        && membership.DateRemoved is null && string.Equals(membership.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase);
    public static string[] Resolve(TenantMembership? membership, UserProfile? profile)
    {
        if (!IsActive(membership)) return [];
        if (membership!.IsOwner || membership.Role.Equals("owner", StringComparison.OrdinalIgnoreCase))
            return Modules.SelectMany(m => new[] { m + ".read", m + ".write" }).ToArray();
        if (profile is not null && (!profile.IsActive || profile.IsDeleted)) return [];
        var role = membership.Role.ToLowerInvariant();
        var defaults = role switch {
            "admin" => Modules,
            "staff" or "member" => Modules.Except(["users", "settings", "billing"]).ToArray(),
            "billing_admin" => new[] { "billing" },
            _ => Array.Empty<string>()
        };
        var allowed = defaults.SelectMany(m => new[] { m + ".read", m + ".write" }).ToArray();
        return profile?.ModulePermissions is null ? allowed : allowed.Intersect(profile.ModulePermissions, StringComparer.Ordinal).ToArray();
    }
    public static bool Allows(IEnumerable<string> permissions, string module, bool write)
    {
        var set = permissions.ToHashSet(StringComparer.Ordinal);
        var action = write ? ".write" : ".read";
        if (!set.Contains(module + ".read") || (write && !set.Contains(module + action))) return false;
        // These aggregate modules can disclose or act on several operational modules.
        if (module is "dashboard" or "bob")
            return Operations.All(m => set.Contains(m + ".read") && (module != "bob" || set.Contains(m + ".write")));
        return true;
    }
    public static string[]? Validate(string[]? permissions)
    {
        if (permissions is null) return null;
        var known = Modules.SelectMany(m => new[] { m + ".read", m + ".write" }).ToHashSet();
        if (permissions.Any(p => !known.Contains(p))) throw new ArgumentException("Unknown module permission.");
        if (permissions.Any(p => p.EndsWith(".write") && !permissions.Contains(p.Replace(".write", ".read"))))
            throw new ArgumentException("Write access also requires read access.");
        return permissions.Distinct().Order().ToArray();
    }
}
