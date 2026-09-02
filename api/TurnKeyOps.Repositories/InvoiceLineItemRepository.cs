using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Repositories.Interfaces;

namespace TurnKeyOps.Repositories;

public class InvoiceLineItemRepository : AzureTablesRepositoryBase<InvoiceLineItem>, IInvoiceLineItemRepository
{
    public InvoiceLineItemRepository(
        IAzureTablesRepositoryStore<InvoiceLineItem> store,
        IMemoryCache cache,
        ITenantContext tenantContext,
        IOptions<RepositoryOptions> repositoryOptions) : base(store, cache, tenantContext, repositoryOptions.Value)
    {
    }

    public async Task<IReadOnlyCollection<InvoiceLineItem>> ListAsync(string partitionKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return (await GetAllAsync(false, false))
            .Where(item => item.PartitionKey == partitionKey && !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .ToArray();
    }
}
