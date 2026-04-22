using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class ActivityItemDefinitionRepository : AzureTablesRepositoryBase<ActivityItemDefinition>, IActivityItemDefinitionRepository
    {
        private readonly IAzureTablesRepositoryStore<ActivityItemDefinition> _azureStore;

        public ActivityItemDefinitionRepository(
            IAzureTablesRepositoryStore<ActivityItemDefinition> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<ActivityItemDefinition>> GetActiveDefinitionsAsync(Guid tenantId, CancellationToken ct = default)
        {
            var pk = $"TENANT|{tenantId}";
            var results = new List<ActivityItemDefinition>();
            await foreach (var item in _azureStore.QueryAsync(e => e.PartitionKey == pk && e.IsActive && !e.IsDeleted, ct, "IsDeleted"))
                results.Add(item);

            return results;
        }
    }
}
