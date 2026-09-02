using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IQuoteRequestRepository : IBaseRepositoryAsync<QuoteRequest>
{
    Task<QuoteRequest?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<QuoteRequest>> ListAsync(string partitionKey, CancellationToken ct = default);
}
