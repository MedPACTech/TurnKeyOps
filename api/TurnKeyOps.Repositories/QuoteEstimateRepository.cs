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
    private readonly VersionedEnvelopeStore<QuoteEstimate>? _rows;
    public QuoteEstimateRepository(
        IAzureTablesRepositoryStore<QuoteEstimate> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        : base(store, cache, tenantContext, repositoryOptions.Value) { _store = store; _rows=configuration is null?null:new(configuration,"QuoteEstimates"); }

    public new async Task<QuoteEstimate> SaveAsync(QuoteEstimate entity, CancellationToken ct = default)
    {
        try
        {
            if(_rows is not null)return await _rows.SaveAsync(entity,string.IsNullOrEmpty(entity.ETag.ToString()),ct);
            return string.IsNullOrEmpty(entity.ETag.ToString())
                ? await _store.AddAsync(null, entity, ct)
                : await _store.UpdateAsync(null, entity, Azure.Data.Tables.TableUpdateMode.Replace, entity.ETag, ct);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status is 409 or 412)
        { throw new InvalidOperationException("The estimate changed. Reload before saving.", ex); }
    }

    public async Task ArchiveAsync(QuoteEstimate entity, CancellationToken ct = default)
    {
        if(string.IsNullOrEmpty(entity.CustomerAccessTokenHash))return;
        var archived=System.Text.Json.JsonSerializer.Deserialize<QuoteEstimate>(System.Text.Json.JsonSerializer.Serialize(entity))!;
        archived.RowKey=$"{entity.RowKey}|issued|{entity.CustomerAccessTokenHash}";archived.ETag=default;
        try {if(_rows is not null)await _rows.SaveAsync(archived,true,ct);else await _store.AddAsync(null,archived,ct);}
        catch(Azure.RequestFailedException ex) when(ex.Status==409)
        {
            // A failed root transition can leave an unsigned archive. Preserve the newest
            // source envelope on retry, including a signature received in the meantime.
            var prior=await GetAsync(archived.PartitionKey,archived.RowKey,ct);
            if(prior is not null && prior.DateUpdated < archived.DateUpdated)
            {
                archived.ETag=prior.ETag;
                if(_rows is not null)await _rows.SaveAsync(archived,false,ct);else await _store.UpdateAsync(null,archived,Azure.Data.Tables.TableUpdateMode.Replace,prior.ETag,ct);
            }
        }
    }
    public Task<QuoteEstimate?> GetArchiveAsync(string partitionKey, Guid requestId, string tokenHash, CancellationToken ct = default)
        => GetAsync(partitionKey,$"{TurnKeyOps.Lib.Utils.RepositoryKeyHelper.ToRowKey(requestId)}|issued|{tokenHash}",ct);
    public Task<QuoteEstimate?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        _rows is not null?_rows.GetAsync(partitionKey,rowKey,ct):_store.GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<QuoteEstimate>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = new List<QuoteEstimate>();
        await foreach (var item in (_rows is not null?_rows.ListAsync(partitionKey,ct):_store.QueryAsync(item => item.PartitionKey == partitionKey, ct)))
            if (!item.IsDeleted && !item.RowKey.Contains("|issued|",StringComparison.Ordinal)) results.Add(item);
        return results.OrderByDescending(item => item.DateUpdated).ToArray();
    }
}
