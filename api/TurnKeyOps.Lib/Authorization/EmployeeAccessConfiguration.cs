using System.Text.Json;
using System.Text.RegularExpressions;

namespace MedInsights.Lib.Authorization;

public sealed class EmployeeAccessRole
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string[] ModulePermissions { get; set; } = [];
}

public sealed class EmployeeAccessConfiguration
{
    public EmployeeAccessRole[] Roles { get; set; } = [];
    public string[] Teams { get; set; } = [];
    // Previous names resolve directly to the current name so renames preserve profile assignments.
    public Dictionary<string, string> TeamAliases { get; set; } = new();
    public string? ResolveTeam(string? name) => name is not null && TeamAliases.TryGetValue(name, out var current) ? current : name;
    public static EmployeeAccessConfiguration Default() => new() {
        Roles = [new() { Id = "technician", Name = "Technician", ModulePermissions =
            ["bob.read", "bob.write", "calendar.read", "calendar.write", "jobs.read", "jobs.write", "inventory.read", "inventory.write", "estimates.read", "estimates.write", "estimates.submit", "contacts.read", "contacts.write"] }]
    };
    public static EmployeeAccessConfiguration Parse(string? json)
    {
        if (json is null) return Default();
        var result = JsonSerializer.Deserialize<EmployeeAccessConfiguration>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new ArgumentException("Enter roles and teams.");
        result.Validate();
        return result;
    }
    public void Validate()
    {
        if (Roles is null || Teams is null || Roles.Length > 100 || Teams.Length > 100) throw new ArgumentException("Configure at most 100 roles and teams.");
        if (Roles.Any(r => r is null || !Regex.IsMatch(r.Id, "^[a-z0-9][a-z0-9-]{0,63}$") || string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100 || r.Name != r.Name.Trim() || r.ModulePermissions is null))
            throw new ArgumentException("Each role needs a unique id, name, and module permissions.");
        if (Roles.Select(r => r.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Roles.Length || Roles.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Roles.Length)
            throw new ArgumentException("Role ids and names must be unique.");
        if (TeamAliases is null || TeamAliases.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 100 || pair.Key != pair.Key.Trim() || Teams.Contains(pair.Key) || !Teams.Contains(pair.Value)))
            throw new ArgumentException("Previous team names must refer to a current team.");
        foreach (var role in Roles) UserModulePermissions.Validate(role.ModulePermissions);
        if (Teams.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 100 || t != t.Trim()) || Teams.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Teams.Length)
            throw new ArgumentException("Team names must be unique and at most 100 characters.");
    }
}
