using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantOnboardingPolicyRepository : IBaseRepositoryAsync<TenantOnboardingPolicy>
    {
        Task<TenantOnboardingPolicy?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
