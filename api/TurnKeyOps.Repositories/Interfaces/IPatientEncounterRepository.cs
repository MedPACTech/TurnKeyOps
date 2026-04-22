using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientEncounterRepository : IBaseRepositoryAsync<PatientEncounter>
    {
        Task<(IEnumerable<PatientEncounter> Results, string? ContinuationToken)> GetEncountersByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default);

        Task<IReadOnlyList<PatientEncounter>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default);
        Task<PatientEncounter?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<PatientEncounter?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default);
    }
}

