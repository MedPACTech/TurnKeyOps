using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IAuditEventRepository : IBaseRepositoryAsync<AuditEvent>
    {
        Task<AuditEvent?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<AuditEvent>> GetByTenantAsync(Guid? tenantId, int take = 100, CancellationToken ct = default);
    }
}
