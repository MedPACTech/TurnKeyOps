using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public sealed class CalendarEventRepository : AzureTablesRepositoryBase<CalendarEvent>, ICalendarEventRepository
{
    private readonly IAzureTablesRepositoryStore<CalendarEvent> _store;
    private readonly VersionedEnvelopeStore<CalendarEvent>? _rows;
    private readonly CalendarReservationStore? _reservations;
    public CalendarEventRepository(
        IAzureTablesRepositoryStore<CalendarEvent> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
        : base(store, cache, tenantContext, repositoryOptions.Value) { _store = store; _rows=configuration is null?null:new(configuration,"CalendarEvents"); _reservations=configuration is null?null:new(configuration); }

    public new async Task<CalendarEvent> SaveAsync(CalendarEvent entity, CancellationToken ct = default)
    {
        try
        {
            if(_reservations is not null)return await _reservations.SaveAsync(entity,ct);
            return string.IsNullOrEmpty(entity.ETag.ToString())
                ? await _store.AddAsync(null, entity, ct)
                : await _store.UpdateAsync(null, entity, Azure.Data.Tables.TableUpdateMode.Replace, entity.ETag, ct);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status is 409 or 412)
        { throw new InvalidOperationException("The record changed. Reload before saving.", ex); }
    }

    public Task<CalendarEvent?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default) =>
        _rows is not null?_rows.GetAsync(partitionKey,rowKey,ct):_store.GetByKeysAsync(partitionKey, rowKey, ct);

    public async Task<IReadOnlyCollection<CalendarEvent>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var results = new List<CalendarEvent>();
        await foreach (var item in (_rows is not null?_rows.ListAsync(partitionKey,ct):_store.QueryAsync(item => item.PartitionKey == partitionKey, ct)))
            if (!item.IsDeleted) results.Add(item);
        return results.OrderByDescending(item => item.DateUpdated).ToArray();
    }
}
