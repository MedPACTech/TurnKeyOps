using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientMedicationRepository : IAzureTablesRepositoryAsync<PatientMedication>
    {
        Task<PatientMedication?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<PatientMedication>> GetByPatientAsync(string partitionKey, Guid patientId);
        Task<IReadOnlyList<PatientMedication>> GetByProviderAsync(string partitionKey, Guid providerId);
    }
}
