using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class ActivityLogRepository : AzureTablesRepositoryBase<ActivityLog>, IActivityLogRepository
    {
        private readonly IAzureTablesRepositoryStore<ActivityLog> _azureStore;

        public ActivityLogRepository(
            IAzureTablesRepositoryStore<ActivityLog> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<ActivityLog?> GetByContextAsync(Guid tenantId, Guid userId, DateTime entryDate, CancellationToken ct = default)
        {
            var pk = $"TENANT|{tenantId}|MONTH|{entryDate:yyyyMM}";
            var prefix = $"DATE|{entryDate:yyyyMMdd}|USER|{userId}|EVENT|";

            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == pk && !e.IsDeleted, ct, "IsDeleted"))
            {
                if (!string.IsNullOrWhiteSpace(entity.RowKey) && entity.RowKey.StartsWith(prefix, StringComparison.Ordinal))
                    return entity;
            }

            return null;
        }

        public async Task UpsertAsync(ActivityLog entity, CancellationToken ct = default)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(entity, ct);
        }
    }
}
