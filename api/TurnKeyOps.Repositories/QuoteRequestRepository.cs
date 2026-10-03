using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class QuoteRequestRepository : AzureTablesRepositoryBase<QuoteRequest>, IQuoteRequestRepository
{
    private readonly IAzureTablesRepositoryStore<QuoteRequest> _store;

    public QuoteRequestRepository(
        IAzureTablesRepositoryStore<QuoteRequest> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions)
        : base(store, cache, tenantContext, repositoryOptions.Value)
    {
        _store = store;
    }

    public Task<QuoteRequest?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<QuoteRequest>> ListAsync(
        string partitionKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = new List<QuoteRequest>();
        await foreach (var item in _store.QueryAsync(item => item.PartitionKey == partitionKey, ct))
        {
            if (!item.IsDeleted) results.Add(item);
        }
        return results.OrderByDescending(item => item.SubmittedAtUtc).ToArray();
    }
}
