using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IProcessingTokenLedgerRepository : IBaseRepositoryAsync<ProcessingTokenLedger>
    {
        Task<ProcessingTokenLedger?> GetByMessageIdAsync(Guid messageId);
        Task<IEnumerable<ProcessingTokenLedger>> GetByTenantAsync(string tenantId);
        Task<IEnumerable<ProcessingTokenLedger>> GetByUserAsync(string tenantId, string userId);
    }
}
