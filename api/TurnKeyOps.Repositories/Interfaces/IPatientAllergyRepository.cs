using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientAllergyRepository : IAzureTablesRepositoryAsync<PatientAllergy>
    {
        Task<IReadOnlyList<PatientAllergy>> GetByPatientAsync(string partitionKey);
        Task<PatientAllergy?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
