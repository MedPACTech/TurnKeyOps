using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientContextRepository : IBaseRepositoryAsync<PatientContext>
    {
        Task<IEnumerable<PatientContext>> GetPatientsAsync(string partitionKey);
        Task<IEnumerable<PatientContext>> GetActivePatientAsync(string partitionKey);
        Task<PatientContext?> GetByPatientIdAsync(string partitionKey, string patientId, CancellationToken ct = default);
        Task<PatientContext?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
