using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IReferralWorkItemRepository : IBaseRepositoryAsync<ReferralWorkItem>
    {
        Task<ReferralWorkItem?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<ReferralWorkItem>> GetByTenantAsync(
            Guid tenantId,
            Guid? patientId = null,
            Guid? encounterId = null,
            string? status = null,
            string? search = null,
            CancellationToken ct = default);
    }
}
