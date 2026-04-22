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
    public sealed class NoteTypeRepository : AzureTablesRepositoryBase<NoteType>, INoteTypeRepository
    {
        public const string SystemPartitionKey = "ENCOUNTERTYPE|SYSTEM";
        public const string DefinitionRecordType = "Definition";
        public const string SystemOverrideRecordType = "SystemOverride";

        private readonly IAzureTablesRepositoryStore<NoteType> _azureStore;

        public NoteTypeRepository(
            IAzureTablesRepositoryStore<NoteType> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<NoteType>> GetSystemDefinitionsAsync(CancellationToken ct = default)
        {
            var results = new List<NoteType>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == SystemPartitionKey
                    && e.RecordType == DefinitionRecordType
                    && e.IsSystem
                    && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<NoteType>> GetTenantCustomDefinitionsAsync(Guid tenantId, CancellationToken ct = default)
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var results = new List<NoteType>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey
                    && e.RecordType == DefinitionRecordType
                    && !e.IsSystem
                    && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<NoteType>> GetTenantSystemOverridesAsync(Guid tenantId, CancellationToken ct = default)
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var results = new List<NoteType>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey
                    && e.RecordType == SystemOverrideRecordType
                    && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<NoteType?> GetSystemDefinitionAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await GetByKeysAsync(SystemPartitionKey, EntityKeyPolicy.Row(id), ct, includeDeleted: false);
            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return entity.RecordType == DefinitionRecordType && entity.IsSystem ? entity : null;
        }

        public async Task<NoteType?> GetTenantCustomDefinitionAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        {
            var entity = await GetByKeysAsync(
                EntityKeyPolicy.TenantPartition(tenantId),
                EntityKeyPolicy.Row(id),
                ct,
                includeDeleted: false);

            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return entity.RecordType == DefinitionRecordType && !entity.IsSystem ? entity : null;
        }

        public async Task<NoteType?> GetTenantSystemOverrideAsync(Guid tenantId, Guid systemNoteTypeId, CancellationToken ct = default)
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey
                    && e.RecordType == SystemOverrideRecordType
                    && e.SystemNoteTypeId == systemNoteTypeId
                    && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                return entity;
            }

            return null;
        }

        private static void SyncEntityIdentityFromKeys(NoteType entity)
        {
            if (entity.Id == Guid.Empty && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
