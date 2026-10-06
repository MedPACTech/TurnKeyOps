using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class JobRepository : AzureTablesRepositoryBase<Job>, IJobRepository
{
    private readonly IAzureTablesRepositoryStore<Job> _store;
    private readonly VersionedEnvelopeStore<Job>? _rows;
    public JobRepository(
        IAzureTablesRepositoryStore<Job> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        : base(store, cache, tenantContext, repositoryOptions.Value) { _store = store; _rows=configuration is null?null:new(configuration,"Jobs"); }

    public new async Task<Job> SaveAsync(Job entity, CancellationToken ct = default)
    {
        try
        {
            if(_rows is not null)return await _rows.SaveAsync(entity,string.IsNullOrEmpty(entity.ETag.ToString()),ct);
            return string.IsNullOrEmpty(entity.ETag.ToString())
                ? await _store.AddAsync(null, entity, ct)
                : await _store.UpdateAsync(null, entity, Azure.Data.Tables.TableUpdateMode.Replace, entity.ETag, ct);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status is 409 or 412)
        { throw new InvalidOperationException("The record changed. Reload before saving.", ex); }
    }

    public Task<Job?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        _rows is not null?_rows.GetAsync(partitionKey,rowKey,ct):_store.GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<Job>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = new List<Job>();
        await foreach (var item in (_rows is not null?_rows.ListAsync(partitionKey,ct):_store.QueryAsync(item => item.PartitionKey == partitionKey, ct)))
            if (!item.IsDeleted) results.Add(item);
        return results.OrderByDescending(item => item.DateUpdated).ToArray();
    }
}
