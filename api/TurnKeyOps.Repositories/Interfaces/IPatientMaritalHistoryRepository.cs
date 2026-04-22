using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientMaritalHistoryRepository : IAzureTablesRepositoryAsync<PatientMaritalHistory>
    {
        Task<IReadOnlyList<PatientMaritalHistory>> GetByPatientAsync(string partitionKey);
        Task<PatientMaritalHistory?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
