using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface ILeadRepository : IBaseRepositoryAsync<Lead>
{
    Task<Lead> CommitAsync(Lead entity, bool create, CancellationToken ct = default);
    Task<Lead?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<Lead>> ListAsync(string partitionKey, CancellationToken ct = default);
}
