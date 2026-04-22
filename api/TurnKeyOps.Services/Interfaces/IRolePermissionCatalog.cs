using MedInsights.Lib.Configurations;

namespace MedInsights.Services.Interfaces
{
    public interface IRolePermissionCatalog
    {
        IReadOnlyList<PermissionDefinitionOption> GetPermissions();
        IReadOnlyList<RolePermissionSeedOption> GetRoleMappings();
    }
}
