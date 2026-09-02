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
    public QuoteRequestRepository(
        IAzureTablesRepositoryStore<QuoteRequest> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions)
        : base(store, cache, tenantContext, repositoryOptions.Value)
    {
    }

    public Task<QuoteRequest?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<QuoteRequest>> ListAsync(
        string partitionKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var all = await GetAllAsync(false, false);
        return all
            .Where(item => item.PartitionKey == partitionKey && !item.IsDeleted)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .ToArray();
    }
}
