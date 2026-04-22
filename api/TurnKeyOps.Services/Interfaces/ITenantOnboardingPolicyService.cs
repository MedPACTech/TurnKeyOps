using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantOnboardingPolicyService
    {
        Task<TenantOnboardingPolicyDto> GetCurrentAsync(CancellationToken ct = default);
        Task<TenantOnboardingPolicyDto> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task<TenantOnboardingPolicyDto> UpdateCurrentAsync(TenantOnboardingPolicyDto dto, CancellationToken ct = default);
    }
}
