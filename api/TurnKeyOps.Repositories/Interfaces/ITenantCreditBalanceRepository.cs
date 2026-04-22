using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantCreditBalanceRepository : IBaseRepositoryAsync<TenantCreditBalance>
    {
        Task<TenantCreditBalance?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
