using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientMilitaryFirstResponderRepository : IAzureTablesRepositoryAsync<PatientMilitaryFirstResponder>
    {
        Task<IReadOnlyList<PatientMilitaryFirstResponder>> GetByPatientAsync(string partitionKey);
        Task<PatientMilitaryFirstResponder?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
    }
}
