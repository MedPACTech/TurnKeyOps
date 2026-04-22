using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class TokenLedgerRepository : AzureTablesRepositoryBase<TokenLedger>, ITokenLedgerRepository
    {
        private readonly IAzureTablesRepositoryStore<TokenLedger> _azureStore;

        public TokenLedgerRepository(
            IAzureTablesRepositoryStore<TokenLedger> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<TokenLedger?> GetLatestByTenantAsync(string tenantId, CancellationToken ct = default)
        {
            TokenLedger? latest = null;
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == tenantId && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentity(entity);
                if (latest == null || string.CompareOrdinal(entity.RowKey, latest.RowKey) > 0)
                    latest = entity;
            }

            return latest;
        }

        public async Task<IEnumerable<TokenLedger>> GetByTenantAsync(string tenantId, CancellationToken ct = default)
        {
            var results = new List<TokenLedger>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == tenantId && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentity(entity);
                results.Add(entity);
            }

            return results.OrderByDescending(x => x.RowKey).ToList();
        }

        public async Task<IEnumerable<TokenLedger>> GetByUserAsync(string tenantId, string userId, CancellationToken ct = default)
        {
            if (!Guid.TryParse(userId, out var parsedUserId))
                return Enumerable.Empty<TokenLedger>();

            var results = new List<TokenLedger>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == tenantId && e.UserId == parsedUserId && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentity(entity);
                results.Add(entity);
            }

            return results.OrderByDescending(x => x.RowKey).ToList();
        }

        private static void SyncEntityIdentity(TokenLedger entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey))
            {
                var firstSegment = entity.RowKey.Split('|', 2)[0];
                if (Guid.TryParse(firstSegment, out var id))
                    entity.Id = id;
            }
        }
    }
}
