using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IInvoiceLineItemRepository : IBaseRepositoryAsync<InvoiceLineItem>
{
    Task<IReadOnlyCollection<InvoiceLineItem>> ListAsync(string partitionKey, CancellationToken ct = default);
}
