using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class ProcessingCreditUsageRepository : AzureTablesRepositoryBase<ProcessingCreditUsage>, IProcessingCreditUsageRepository
    {
        private readonly IAzureTablesRepositoryStore<ProcessingCreditUsage> _azureStore;

        public ProcessingCreditUsageRepository(
            IAzureTablesRepositoryStore<ProcessingCreditUsage> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<ProcessingCreditUsage?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(e => e.RequestId == requestId && !e.IsDeleted, softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentity(entity);
                return entity;
            }

            return null;
        }

        private static void SyncEntityIdentity(ProcessingCreditUsage entity)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = entity.RequestId;
        }
    }
}
