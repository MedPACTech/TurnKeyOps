using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public class JobSiteRepository : AzureTablesRepositoryBase<JobSite>, IJobSiteRepository
{
    public JobSiteRepository(
        IAzureTablesRepositoryStore<JobSite> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions) : base(store, cache, tenantContext, repositoryOptions.Value)
    {
    }
    public Task<JobSite?> GetAsync(string partitionKey,string rowKey,CancellationToken ct=default)=>GetByKeysAsync(partitionKey,rowKey,ct);
}
