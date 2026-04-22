using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IUserProfileService
    {
        Task<UserProfileDto?> GetCurrentAsync(CancellationToken ct = default);
        Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken ct = default);
        Task<UserProfileDto> EnsureProfileExistsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
        Task<IEnumerable<UserProfileDto>> GetAllAsync(CancellationToken ct = default);
        Task<UserProfileDto> UpdateAsync(UserProfileDto dto, CancellationToken ct = default);
        Task DeleteAsync(UserProfileDto dto, CancellationToken ct = default);
        Task CreateUserProfileAsync(Guid tenantId, Guid userId, CancellationToken ct);
    }
}
