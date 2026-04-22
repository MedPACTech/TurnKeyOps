using MedInsights.Lib.Dtos;
using System.Text.Json;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantProfileService
    {
        Task<TenantProfileDto?> GetAsync(Guid id, CancellationToken ct = default);
        Task<TenantProfileDto?> GetCurrentAsync(CancellationToken ct = default);
        Task<TenantProfileDto> EnsureProfileExistsAsync(CancellationToken ct = default);
        Task<TenantProfileDto> CreateAsync(TenantProfileDto dto, CancellationToken ct = default);
        Task<TenantProfileDto> UpdateAsync(Guid id, JsonElement payload, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
