using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientOrderRepository : IAzureTablesRepositoryAsync<PatientOrder>
    {
        Task<PatientOrder?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<PatientOrder>> GetByPatientAsync(string partitionKey, Guid patientId);
        Task<IReadOnlyList<PatientOrder>> GetByProviderAsync(string partitionKey, Guid providerId);
    }
}
