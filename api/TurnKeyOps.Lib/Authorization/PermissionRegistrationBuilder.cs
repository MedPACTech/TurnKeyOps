using MedInsights.Lib.Configurations;

namespace MedInsights.Lib.Authorization
{
    public sealed class PermissionRegistrationBuilder
    {
        private readonly List<PermissionDefinitionOption> _permissions = new();
        private readonly List<RolePermissionSeedOption> _roleMappings = new();

        public PermissionRegistrationBuilder AddPermission(string key, Guid? id = null, string? name = null, string? description = null)
        {
            _permissions.Add(new PermissionDefinitionOption
            {
                Key = key,
                Id = id,
                Name = name,
                Description = description
            });
            return this;
        }

        public PermissionRegistrationBuilder MapRole(string roleKey, params string[] permissionKeys)
        {
            _roleMappings.Add(new RolePermissionSeedOption
            {
                RoleKey = roleKey,
                PermissionKeys = permissionKeys.ToList()
            });
            return this;
        }

        public IReadOnlyList<PermissionDefinitionOption> Permissions => _permissions;
        public IReadOnlyList<RolePermissionSeedOption> RoleMappings => _roleMappings;
    }
}
