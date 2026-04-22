using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class UserProfileRepository : AzureTablesRepositoryBase<UserProfile>, IUserProfileRepository
    {
        private readonly IAzureTablesRepositoryStore<UserProfile> _azureStore;

        public UserProfileRepository(
            IAzureTablesRepositoryStore<UserProfile> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<UserProfile?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IReadOnlyList<Guid>> GetTenantIdsAsync(CancellationToken ct = default)
        {
            var ids = new HashSet<Guid>();

            await foreach (var entity in _azureStore.QueryAsync(e => !e.IsDeleted, ct, "IsDeleted"))
            {
                if (TryGetTenantId(entity.PartitionKey, out var tenantId))
                    ids.Add(tenantId);
            }

            return ids.ToList();
        }

        public async Task<(IEnumerable<UserProfile> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default)
        {
            var (results, nextToken) = await _azureStore.GetByPartitionPagedAsync(partitionKey, pageSize, continuationToken, ct, "IsDeleted");
            foreach (var item in results)
                SyncEntityIdentityFromKeys(item);

            return (results, nextToken);
        }

        private static void SyncEntityIdentityFromKeys(UserProfile entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }

        private static bool TryGetTenantId(string? partitionKey, out Guid tenantId)
        {
            tenantId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(partitionKey))
                return false;

            if (Guid.TryParse(partitionKey, out tenantId) && tenantId != Guid.Empty)
                return true;

            var segments = partitionKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (!segment.StartsWith("TENANT=", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = segment["TENANT=".Length..];
                if (Guid.TryParse(value, out tenantId) && tenantId != Guid.Empty)
                    return true;
            }

            return false;
        }
    }
}
