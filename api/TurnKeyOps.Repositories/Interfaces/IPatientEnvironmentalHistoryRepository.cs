using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientEnvironmentalHistoryRepository : IAzureTablesRepositoryAsync<PatientEnvironmentalHistory>
    {
        Task<IReadOnlyList<PatientEnvironmentalHistory>> GetByPatientAsync(string partitionKey);
        Task<PatientEnvironmentalHistory?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
