using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IInvoiceRepository : IBaseRepositoryAsync<Invoice>
{
    Task<Invoice?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<Invoice>> ListAsync(string partitionKey, CancellationToken ct = default);
}
