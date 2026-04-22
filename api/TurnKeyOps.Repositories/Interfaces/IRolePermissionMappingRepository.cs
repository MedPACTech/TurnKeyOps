using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IRolePermissionMappingRepository : IBaseRepositoryAsync<RolePermissionMapping>
    {
        Task<IReadOnlyList<RolePermissionMapping>> GetSystemMappingsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<RolePermissionMapping>> GetTenantMappingsAsync(Guid tenantId, CancellationToken ct = default);
        Task<IReadOnlyList<RolePermissionMapping>> GetMappingsForRoleAsync(Guid? tenantId, Guid roleId, CancellationToken ct = default);
    }
}
