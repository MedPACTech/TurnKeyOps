using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IFacilityPatientAssignmentRepository : IBaseRepositoryAsync<FacilityPatientAssignment>
    {
        Task<FacilityPatientAssignment?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<FacilityPatientAssignment>> GetByFacilityAsync(string partitionKey, CancellationToken ct = default);
        Task<FacilityPatientAssignment?> GetActiveByPatientAsync(string partitionKey, Guid patientId, CancellationToken ct = default);
    }
}
