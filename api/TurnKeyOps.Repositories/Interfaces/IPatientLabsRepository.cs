using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientLabsRepository : IAzureTablesRepositoryAsync<PatientLabs>
    {
        Task<IReadOnlyList<PatientLabs>> GetByPatientAsync(string partitionKey);
        Task<PatientLabs?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
