using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public class EstimateDefaultsRepository : AzureTablesRepositoryBase<EstimateDefaultsProfile>, IEstimateDefaultsRepository
{
    public EstimateDefaultsRepository(
        IAzureTablesRepositoryStore<EstimateDefaultsProfile> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions) : base(store, cache, tenantContext, repositoryOptions.Value)
    {
    }

    public async Task<EstimateDefaultsProfile?> GetAsync(
        string partitionKey,
        string rowKey,
        CancellationToken ct = default,
        bool includeDeleted = false) =>
        await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
}
