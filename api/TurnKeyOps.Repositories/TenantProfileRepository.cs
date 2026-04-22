using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class TenantProfileRepository : AzureTablesRepositoryBase<TenantProfile>, ITenantProfileRepository
    {
        private readonly IAzureTablesRepositoryStore<TenantProfile> _azureStore;

        public TenantProfileRepository(
            IAzureTablesRepositoryStore<TenantProfile> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<TenantProfile?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IReadOnlyList<Guid>> GetTenantIdsAsync(CancellationToken ct = default)
        {
            var ids = new HashSet<Guid>();
            await foreach (var entity in _azureStore.QueryAsync(e => !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                if (entity.Id != Guid.Empty)
                    ids.Add(entity.Id);
            }

            return ids.ToList();
        }

        private static void SyncEntityIdentityFromKeys(TenantProfile entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
