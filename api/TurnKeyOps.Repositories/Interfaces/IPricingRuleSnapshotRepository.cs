using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPricingRuleSnapshotRepository : IBaseRepositoryAsync<PricingRuleSnapshot>
    {
        Task<PricingRuleSnapshot?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
