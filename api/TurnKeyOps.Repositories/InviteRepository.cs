using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class InviteRepository : AzureTablesRepositoryBase<Invite>, IInviteRepository
    {
        private readonly IAzureTablesRepositoryStore<Invite> _azureStore;

        public InviteRepository(
            IAzureTablesRepositoryStore<Invite> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<Invite?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct = default, bool includeDeleted = false)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.Id == id && (includeDeleted || !e.IsDeleted),
                               ct,
                               "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                return entity;
            }

            return null;
        }

        public async Task<(IEnumerable<Invite> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default)
            => await _azureStore.GetByPartitionPagedAsync(partitionKey, pageSize, continuationToken, ct, "IsDeleted");

        private static void SyncEntityIdentityFromKeys(Invite entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
