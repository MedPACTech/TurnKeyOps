using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IEstimateRepository : IBaseRepositoryAsync<Estimate>
{
    Task<Estimate?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
}
