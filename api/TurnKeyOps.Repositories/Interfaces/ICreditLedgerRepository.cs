using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ICreditLedgerRepository : IBaseRepositoryAsync<CreditLedger>
    {
        Task<CreditLedger?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<CreditLedger>> GetByTenantAsync(Guid tenantId, int take = 100, CancellationToken ct = default);
    }
}
