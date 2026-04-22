using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IEstimateDefaultsRepository : IBaseRepositoryAsync<EstimateDefaultsProfile>
{
    Task<EstimateDefaultsProfile?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
}
