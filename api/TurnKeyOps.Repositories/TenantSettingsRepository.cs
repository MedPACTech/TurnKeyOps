using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class TenantSettingsRepository : AzureTablesRepositoryBase<TenantSettingsDocument>, ITenantSettingsRepository
{
    public TenantSettingsRepository(
        IAzureTablesRepositoryStore<TenantSettingsDocument> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions)
        : base(store, cache, tenantContext, repositoryOptions.Value)
    {
    }

    public Task<TenantSettingsDocument?> GetAsync(
        string partitionKey,
        string rowKey,
        CancellationToken ct = default,
        bool includeDeleted = false) =>
        GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
}
