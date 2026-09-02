using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public class JobRepository : AzureTablesRepositoryBase<Job>, IJobRepository
{
    public JobRepository(
        IAzureTablesRepositoryStore<Job> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions) : base(store, cache, tenantContext, repositoryOptions.Value)
    {
    }

    public Task<Job?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<Job>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return (await GetAllAsync(false, false))
            .Where(item => item.PartitionKey == partitionKey && !item.IsDeleted)
            .OrderByDescending(item => item.DateUpdated)
            .ToArray();
    }
}
