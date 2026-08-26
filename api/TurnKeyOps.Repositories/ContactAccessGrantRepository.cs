using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class ContactAccessGrantRepository : AzureTablesRepositoryBase<ContactAccessGrant>, IContactAccessGrantRepository
{
    private readonly IAzureTablesRepositoryStore<ContactAccessGrant> _store;

    public ContactAccessGrantRepository(
        IAzureTablesRepositoryStore<ContactAccessGrant> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions)
        : base(store, cache, tenantContext, repositoryOptions.Value)
    {
        _store = store;
    }

    public Task<ContactAccessGrant?> GetAsync(
        string partitionKey,
        string rowKey,
        CancellationToken ct = default,
        bool includeDeleted = false) =>
        GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

    public async Task<IReadOnlyList<ContactAccessGrant>> ListByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var partitionKey = TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToTenantPartitionKey(tenantId);
        var results = new List<ContactAccessGrant>();
        await foreach (var entity in _store.QueryAsync(
                           item => item.PartitionKey == partitionKey && !item.IsDeleted,
                           ct,
                           "PartitionKey"))
        {
            results.Add(entity);
        }

        return results.OrderBy(item => item.ContactId, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
