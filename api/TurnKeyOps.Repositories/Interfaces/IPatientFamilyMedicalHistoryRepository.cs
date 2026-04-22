using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientFamilyMedicalHistoryRepository : IAzureTablesRepositoryAsync<PatientFamilyMedicalHistory>
    {
        Task<IReadOnlyList<PatientFamilyMedicalHistory>> GetByPatientAsync(string partitionKey);
        Task<PatientFamilyMedicalHistory?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
