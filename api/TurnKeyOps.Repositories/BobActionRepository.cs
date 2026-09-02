using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class BobActionRepository : AzureTablesRepositoryBase<BobActionRecord>, IBobActionRepository
{
    private readonly IAzureTablesRepositoryStore<BobActionRecord> _store;

    public BobActionRepository(
        IAzureTablesRepositoryStore<BobActionRecord> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions)
        : base(store, cache, tenantContext, repositoryOptions.Value)
    {
        _store = store;
    }

    public Task<BobActionRecord?> GetAsync(string partitionKey, Guid actionId, CancellationToken ct = default) =>
        GetByKeysAsync(partitionKey, ActionRowKey(actionId), ct);

    public async Task<BobActionRecord?> FindByIdempotencyKeyAsync(
        string partitionKey,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        await foreach (var item in _store.QueryAsync(
                           value => value.PartitionKey == partitionKey && !value.IsDeleted,
                           ct,
                           "PartitionKey"))
        {
            if (string.Equals(item.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    public async Task<IReadOnlyList<BobActionRecord>> ListByConversationAsync(
        string partitionKey,
        Guid conversationId,
        CancellationToken ct = default)
    {
        var results = new List<BobActionRecord>();
        await foreach (var item in _store.QueryAsync(
                           value => value.PartitionKey == partitionKey && !value.IsDeleted,
                           ct,
                           "PartitionKey"))
        {
            if (item.ConversationId == conversationId)
                results.Add(item);
        }

        return results.OrderByDescending(item => item.ProposedAtUtc).ToList();
    }

    public static string ActionRowKey(Guid id) => $"ACTION={id:N}";
}
