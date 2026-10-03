using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public class CustomerRepository : AzureTablesRepositoryBase<Customer>, ICustomerRepository
{
    private readonly IAzureTablesRepositoryStore<Customer> _store;

    public CustomerRepository(
        IAzureTablesRepositoryStore<Customer> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions) : base(store, cache, tenantContext, repositoryOptions.Value)
    {
        _store = store;
    }

    public Task<Customer?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<Customer>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        var results = new List<Customer>();
        await foreach (var item in _store.QueryAsync(item => item.PartitionKey == partitionKey, ct))
        {
            if (!item.IsDeleted) results.Add(item);
        }
        return results;
    }
}
