using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;

namespace MedInsights.Authorization.Tests.Infrastructure;

internal sealed class TestRolePermissionCatalog : IRolePermissionCatalog
{
    private readonly IReadOnlyList<PermissionDefinitionOption> _permissions;
    private readonly IReadOnlyList<RolePermissionSeedOption> _mappings;

    public TestRolePermissionCatalog(
        IEnumerable<PermissionDefinitionOption>? permissions = null,
        IEnumerable<RolePermissionSeedOption>? mappings = null)
    {
        _permissions = permissions?.ToList() ?? [];
        _mappings = mappings?.ToList() ?? [];
    }

    public IReadOnlyList<PermissionDefinitionOption> GetPermissions() => _permissions;

    public IReadOnlyList<RolePermissionSeedOption> GetRoleMappings() => _mappings;
}
