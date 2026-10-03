using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface ICustomerRepository : IBaseRepositoryAsync<Customer>
{
    Task<Customer?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<Customer>> ListAsync(string partitionKey, CancellationToken ct = default);
}
