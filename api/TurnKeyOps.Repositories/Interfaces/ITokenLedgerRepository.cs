using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITokenLedgerRepository : IBaseRepositoryAsync<TokenLedger>
    {
        Task<TokenLedger?> GetLatestByTenantAsync(string tenantId, CancellationToken ct = default);
        Task<IEnumerable<TokenLedger>> GetByTenantAsync(string tenantId, CancellationToken ct = default);
        Task<IEnumerable<TokenLedger>> GetByUserAsync(string tenantId, string userId, CancellationToken ct = default);
    }
}
