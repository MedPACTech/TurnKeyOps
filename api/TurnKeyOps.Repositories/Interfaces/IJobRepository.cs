using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IJobRepository : IBaseRepositoryAsync<Job>
{
    Task<Job?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<Job>> ListAsync(string partitionKey, CancellationToken ct = default);
}
