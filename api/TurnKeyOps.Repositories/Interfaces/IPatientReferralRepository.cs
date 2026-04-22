using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientReferralRepository : IBaseRepositoryAsync<PatientReferral>
    {
        Task<IReadOnlyList<PatientReferral>> GetByPatientAsync(string partitionKey);
        Task<IReadOnlyList<PatientReferral>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task<PatientReferral?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<PatientReferral?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default);
        Task<PatientReferral?> GetByCaptureDraftNoteIdAsync(Guid tenantId, Guid captureDraftNoteId, CancellationToken ct = default);
    }
}

