using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientVitalsRepository : IBaseRepositoryAsync<PatientVitals>
    {
        Task<IReadOnlyList<PatientVitals>> GetByPatientAsync(string partitionKey);
        Task<PatientVitals?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
