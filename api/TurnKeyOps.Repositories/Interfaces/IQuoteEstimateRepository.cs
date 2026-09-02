using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IQuoteEstimateRepository : IBaseRepositoryAsync<QuoteEstimate>
{
    Task<QuoteEstimate?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<QuoteEstimate>> ListAsync(string partitionKey, CancellationToken ct = default);
}
