using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientRepository : IBaseRepositoryAsync<Patient>
    {
        Task<Patient?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IReadOnlyList<Patient>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default);
        Task<(IEnumerable<Patient> Results, string? ContinuationToken)> GetByPartitionPagedAsync(string partitionKey, int pageSize, string? continuationToken = null, CancellationToken ct = default);
        Task<List<Patient>> SearchPatientAsync(string tenantId, string[] rawTerms);
    }
}
