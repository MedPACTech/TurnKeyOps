using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantRoleDefinitionService
    {
        Task<IReadOnlyList<TenantRoleDto>> GetAllAsync(CancellationToken ct = default);
        Task<IReadOnlyList<TenantRoleDto>> GetAssignableAsync(CancellationToken ct = default);
        Task<IReadOnlyList<PermissionDefinitionDto>> GetPermissionCatalogAsync(CancellationToken ct = default);
        Task<TenantRoleDto> CreateAsync(UpsertTenantRoleRequestDto dto, CancellationToken ct = default);
        Task<TenantRoleDto> UpdateAsync(Guid id, UpsertTenantRoleRequestDto dto, CancellationToken ct = default);
        Task<TenantRoleDto> UpdatePermissionsAsync(Guid id, UpdateRolePermissionsRequestDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
