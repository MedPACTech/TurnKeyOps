using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientBillingNoteRepository : IBaseRepositoryAsync<PatientBillingNote>
    {
        Task<IReadOnlyList<PatientBillingNote>> GetByPatientAsync(string partitionKey);
        Task<PatientBillingNote?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<PatientBillingNote?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default);
    }
}

