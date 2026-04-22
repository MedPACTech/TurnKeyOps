using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientNoteRepository : IBaseRepositoryAsync<PatientNote>
    {
        Task<IReadOnlyList<PatientNote>> GetByPatientIdAsync(string partitionKey);
        Task<PatientNote?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<PatientNote?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default);
    }
}
