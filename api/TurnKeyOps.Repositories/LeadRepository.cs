using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class LeadRepository : AzureTablesRepositoryBase<Lead>, ILeadRepository
{
    private readonly IAzureTablesRepositoryStore<Lead> _store;
    private readonly VersionedEnvelopeStore<Lead>? _rows;

    public LeadRepository(
        IAzureTablesRepositoryStore<Lead> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        : base(store, cache, tenantContext, repositoryOptions.Value)
    {
        _store = store;
        _rows=configuration is null?null:new(configuration,"Leads");
    }

    public async Task<Lead> CommitAsync(Lead entity, bool create, CancellationToken ct = default)
    {
        try
        {
            if(_rows is not null)return await _rows.SaveAsync(entity,create,ct);
            return create ? await _store.AddAsync(null, entity, ct)
                : await _store.UpdateAsync(null, entity, Azure.Data.Tables.TableUpdateMode.Replace, entity.ETag, ct);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            throw new InvalidOperationException("This lead changed. Refresh before saving.", ex);
        }
    }

    public Task<Lead?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        _rows is not null?_rows.GetAsync(partitionKey,rowKey,ct):_store.GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<Lead>> ListAsync(
        string partitionKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = new List<Lead>();
        await foreach (var item in (_rows is not null?_rows.ListAsync(partitionKey,ct):_store.QueryAsync(item => item.PartitionKey == partitionKey, ct)))
        {
            if (!item.IsDeleted) results.Add(item);
        }
        return results.OrderByDescending(item => item.DateUpdated).ToArray();
    }
}
