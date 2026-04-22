using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class ProcessingTokenLedgerRepository : AzureTablesRepositoryBase<ProcessingTokenLedger>, IProcessingTokenLedgerRepository
    {
        private readonly IAzureTablesRepositoryStore<ProcessingTokenLedger> _azureStore;

        public ProcessingTokenLedgerRepository(
            IAzureTablesRepositoryStore<ProcessingTokenLedger> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<ProcessingTokenLedger?> GetByMessageIdAsync(Guid messageId)
        {
            await foreach (var entity in _azureStore.QueryAsync(e => e.MessageId == messageId && !e.IsDeleted, softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentity(entity);
                return entity;
            }
            return null;
        }

        public async Task<IEnumerable<ProcessingTokenLedger>> GetByTenantAsync(string tenantId)
        {
            var results = new List<ProcessingTokenLedger>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == tenantId && !e.IsDeleted, softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentity(entity);
                results.Add(entity);
            }
            return results;
        }

        public async Task<IEnumerable<ProcessingTokenLedger>> GetByUserAsync(string tenantId, string userId)
        {
            var parsedUserId = Guid.Parse(userId);
            var results = new List<ProcessingTokenLedger>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == tenantId && e.UserId == parsedUserId && !e.IsDeleted, softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentity(entity);
                results.Add(entity);
            }
            return results;
        }

        private static void SyncEntityIdentity(ProcessingTokenLedger entity)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = entity.MessageId;
        }
    }
}
