using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class AppointmentTypeRepository : AzureTablesRepositoryBase<AppointmentTypeDefinition>, IAppointmentTypeRepository
    {
        private readonly IAzureTablesRepositoryStore<AppointmentTypeDefinition> _azureStore;

        public AppointmentTypeRepository(
            IAzureTablesRepositoryStore<AppointmentTypeDefinition> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<AppointmentTypeDefinition>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await GetByTenantAsync(tenantId, includeDeleted: false, ct);

        public async Task<IReadOnlyList<AppointmentTypeDefinition>> GetByTenantAsync(Guid tenantId, bool includeDeleted, CancellationToken ct = default)
        {
            var results = new List<AppointmentTypeDefinition>();
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);

            if (includeDeleted)
            {
                await foreach (var entity in _azureStore.QueryAsync(
                    e => e.PartitionKey == partitionKey,
                    ct,
                    "IsDeleted"))
                {
                    SyncEntityIdentityFromKeys(entity);
                    results.Add(entity);
                }
            }
            else
            {
                await foreach (var entity in _azureStore.QueryAsync(
                    e => e.PartitionKey == partitionKey && !e.IsDeleted,
                    ct,
                    "IsDeleted"))
                {
                    SyncEntityIdentityFromKeys(entity);
                    results.Add(entity);
                }
            }

            return results;
        }

        public async Task<AppointmentTypeDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool includeDeleted = false)
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var rowKey = EntityKeyPolicy.Row(id);
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        private static void SyncEntityIdentityFromKeys(AppointmentTypeDefinition entity)
        {
            if (entity.Id == Guid.Empty && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
