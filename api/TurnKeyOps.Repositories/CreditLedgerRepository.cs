using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class CreditLedgerRepository : AzureTablesRepositoryBase<CreditLedger>, ICreditLedgerRepository
    {
        private readonly IAzureTablesRepositoryStore<CreditLedger> _azureStore;

        public CreditLedgerRepository(
            IAzureTablesRepositoryStore<CreditLedger> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<CreditLedger?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<IReadOnlyList<CreditLedger>> GetByTenantAsync(Guid tenantId, int take = 100, CancellationToken ct = default)
        {
            var results = new List<CreditLedger>();
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.TenantId == tenantId && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                results.Add(entity);
            }

            return results
                .OrderByDescending(x => x.RowKey)
                .Take(take)
                .ToList();
        }
    }
}
