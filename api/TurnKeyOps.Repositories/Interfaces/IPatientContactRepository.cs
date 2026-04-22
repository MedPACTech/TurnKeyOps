using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientContactRepository : IAzureTablesRepositoryAsync<PatientContact>
    {
        Task<IReadOnlyList<PatientContact>> GetByPatientAsync(string partitionKey);
        Task<PatientContact?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
