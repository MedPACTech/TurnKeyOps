using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantMembershipService
    {
        Task<IEnumerable<TenantMembershipDto>> GetAllAsync(CancellationToken ct = default);
        Task<TenantMembershipDto?> GetAsync(Guid id, CancellationToken ct = default);
        Task<TenantMembershipDto> UpsertAsync(TenantMembershipDto dto, CancellationToken ct = default);
        Task<TenantMembershipDto> UpdateRoleAsync(Guid membershipId, UpdateMembershipRoleRequestDto dto, CancellationToken ct = default);
        Task<TenantMembershipDto> ReassignAsync(Guid membershipId, ReassignMembershipRequestDto dto, CancellationToken ct = default);
        Task<TenantMembershipDto> RemoveAsync(Guid membershipId, CancellationToken ct = default);
    }
}
