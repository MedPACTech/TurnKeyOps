using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IOperationalAlertRepository : IBaseRepositoryAsync<OperationalAlert>
    {
        Task<OperationalAlert?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<OperationalAlert?> GetByDedupeKeyAsync(string partitionKey, string dedupeKey, CancellationToken ct = default);
        Task<IReadOnlyList<OperationalAlert>> GetByTenantAsync(Guid? tenantId, string? status = null, int take = 100, CancellationToken ct = default);
    }
}
