using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public class InvoiceRepository : AzureTablesRepositoryBase<Invoice>, IInvoiceRepository
{
    private readonly IAzureTablesRepositoryStore<Invoice> _store;
    private readonly VersionedEnvelopeStore<Invoice>? _rows;
    public InvoiceRepository(
        IAzureTablesRepositoryStore<Invoice> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions, Microsoft.Extensions.Configuration.IConfiguration? configuration = null) : base(store, cache, tenantContext, repositoryOptions.Value)
    {
        _store=store;
        _rows=configuration is null?null:new(configuration,"Invoices");
    }

    // Preserve physical envelope ETags for payload/outbox-pointer conditional commits, just as
    // the existing accepted Estimate and Job repositories do. The table/JSON shape is unchanged.
    public new async Task<Invoice> SaveAsync(Invoice entity,CancellationToken ct=default)
    {
        try
        {
            if(_rows is not null)return await _rows.SaveAsync(entity,string.IsNullOrEmpty(entity.ETag.ToString()),ct);
            return string.IsNullOrEmpty(entity.ETag.ToString())
                ?await _store.AddAsync(null,entity,ct)
                :await _store.UpdateAsync(null,entity,Azure.Data.Tables.TableUpdateMode.Replace,entity.ETag,ct);
        }
        catch(Azure.RequestFailedException ex)when(ex.Status is 409 or 412)
        {throw new InvalidOperationException("Invoice changed. Refresh before retrying.",ex);}
    }

    public Task<Invoice?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        _rows is not null?_rows.GetAsync(partitionKey,rowKey,ct):_store.GetByKeysAsync(partitionKey,rowKey,ct);

    public async Task<IReadOnlyCollection<Invoice>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if(_rows is not null)
        {
            var items=new List<Invoice>();
            await foreach(var item in _rows.ListAsync(partitionKey,ct))if(!item.IsDeleted)items.Add(item);
            return items.OrderByDescending(item=>item.DateUpdated).ToArray();
        }
        return (await GetAllAsync(false, false))
            .Where(item => item.PartitionKey == partitionKey && !item.IsDeleted)
            .OrderByDescending(item => item.DateUpdated)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<Invoice>> ListFinanceDispatchPendingAsync(int limit,CancellationToken ct=default)
    {
        limit=Math.Clamp(limit,1,250);
        bool Pending(Invoice item)=>!item.IsDeleted&&item.FinanceDispatchPending&&(!item.FinanceDispatchAttemptedAtUtc.HasValue||item.FinanceDispatchAttemptedAtUtc<DateTime.UtcNow.AddSeconds(-30));
        if(_rows is null)return(await GetAllAsync(false,false)).Where(Pending).OrderBy(i=>i.FinanceDispatchAttemptedAtUtc??DateTime.MinValue).Take(limit).ToArray();
        var items=new PriorityQueue<Invoice,long>();
        // Bypass general repository caches: durable dispatch must discover fresh committed pointers.
        await foreach(var item in _rows.ListAllAsync(ct))
        {
            if(!Pending(item))continue;
            items.Enqueue(item,-(item.FinanceDispatchAttemptedAtUtc?.Ticks??0));
            if(items.Count>limit)items.Dequeue();
        }
        return items.UnorderedItems.Select(i=>i.Element).OrderBy(i=>i.FinanceDispatchAttemptedAtUtc??DateTime.MinValue).ToArray();
    }
}
