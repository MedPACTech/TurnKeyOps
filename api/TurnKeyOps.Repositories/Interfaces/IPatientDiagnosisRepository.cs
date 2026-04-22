using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientDiagnosisRepository : IAzureTablesRepositoryAsync<PatientDiagnosis>
    {
        Task<IReadOnlyList<PatientDiagnosis>> GetByPatientAsync(string partitionKey);
        Task<PatientDiagnosis?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
