using MedInsights.Lib.Entities;

namespace MedInsights.Services.Interfaces
{
    public interface IRoleDirectoryService
    {
        string NormalizeRoleKey(string roleKey);
        Task<TenantRoleDefinition?> GetRoleAsync(Guid tenantId, string roleKey, CancellationToken ct = default);
        Task<TenantRoleDefinition> GetRequiredAssignableRoleAsync(Guid tenantId, string roleKey, CancellationToken ct = default);
    }
}
