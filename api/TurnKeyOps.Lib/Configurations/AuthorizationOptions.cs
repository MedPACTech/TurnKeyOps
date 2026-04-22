namespace MedInsights.Lib.Configurations
{
    public sealed class AuthorizationOptions
    {
        public List<PermissionDefinitionOption> Permissions { get; set; } = new();
        public List<RolePermissionSeedOption> RoleMappings { get; set; } = new();
    }

    public sealed class PermissionDefinitionOption
    {
        public string Key { get; set; } = string.Empty;
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public sealed class RolePermissionSeedOption
    {
        public string RoleKey { get; set; } = string.Empty;
        public Guid? RoleId { get; set; }
        public List<string> PermissionKeys { get; set; } = new();
        public List<Guid> PermissionIds { get; set; } = new();
    }
}
