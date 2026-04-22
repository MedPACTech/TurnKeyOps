using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantRoleDefinitionRepository : IBaseRepositoryAsync<TenantRoleDefinition>
    {
        Task<TenantRoleDefinition?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<TenantRoleDefinition?> GetSystemByKeyAsync(string key, CancellationToken ct = default);
        Task<TenantRoleDefinition?> GetTenantByKeyAsync(Guid tenantId, string key, CancellationToken ct = default);
        Task<IReadOnlyList<TenantRoleDefinition>> GetSystemRolesAsync(CancellationToken ct = default);
        Task<IReadOnlyList<TenantRoleDefinition>> GetTenantRolesAsync(Guid tenantId, CancellationToken ct = default);
    }
}
