using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IQuoteEstimateRepository : IBaseRepositoryAsync<QuoteEstimate>
{
    Task ArchiveAsync(QuoteEstimate entity, CancellationToken ct = default);
    Task<QuoteEstimate?> GetArchiveAsync(string partitionKey, Guid requestId, string tokenHash, CancellationToken ct = default);
    Task<QuoteEstimate?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<QuoteEstimate>> ListAsync(string partitionKey, CancellationToken ct = default);
}
