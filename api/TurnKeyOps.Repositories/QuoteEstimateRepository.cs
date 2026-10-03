using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class QuoteEstimateRepository : AzureTablesRepositoryBase<QuoteEstimate>, IQuoteEstimateRepository
{
    private readonly IAzureTablesRepositoryStore<QuoteEstimate> _store;
    public QuoteEstimateRepository(
        IAzureTablesRepositoryStore<QuoteEstimate> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions)
        : base(store, cache, tenantContext, repositoryOptions.Value) { _store = store; }

    public Task<QuoteEstimate?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<QuoteEstimate>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = new List<QuoteEstimate>();
        await foreach (var item in _store.QueryAsync(item => item.PartitionKey == partitionKey, ct))
            if (!item.IsDeleted) results.Add(item);
        return results.OrderByDescending(item => item.DateUpdated).ToArray();
    }
}
