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
    public sealed class NoteTypeProfileRepository : AzureTablesRepositoryBase<NoteTypeProfile>, INoteTypeProfileRepository
    {
        public const string SystemPartitionKey = "NOTETYPEPROFILE|SYSTEM";
        public const string ProfileRecordType = "Profile";

        private readonly IAzureTablesRepositoryStore<NoteTypeProfile> _azureStore;

        public NoteTypeProfileRepository(
            IAzureTablesRepositoryStore<NoteTypeProfile> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<NoteTypeProfile>> GetSystemProfilesAsync(CancellationToken ct = default)
        {
            var results = new List<NoteTypeProfile>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == SystemPartitionKey
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

        public async Task<IReadOnlyList<NoteTypeProfile>> GetTenantProfilesAsync(Guid tenantId, CancellationToken ct = default)
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var results = new List<NoteTypeProfile>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey
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

        public async Task<NoteTypeProfile?> GetSystemProfileAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await GetByKeysAsync(SystemPartitionKey, EntityKeyPolicy.Row(id), ct, includeDeleted: false);
            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return entity.IsSystem ? entity : null;
        }

        public async Task<NoteTypeProfile?> GetTenantProfileAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        {
            var entity = await GetByKeysAsync(
                EntityKeyPolicy.TenantPartition(tenantId),
                EntityKeyPolicy.Row(id),
                ct,
                includeDeleted: false);

            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return !entity.IsSystem ? entity : null;
        }

        public async Task<NoteTypeProfile?> GetSystemProfileByNoteTypeIdAsync(Guid noteTypeId, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == SystemPartitionKey
                    && e.NoteTypeId == noteTypeId
                    && e.IsSystem
                    && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                return entity;
            }

            return null;
        }

        public async Task<NoteTypeProfile?> GetTenantProfileByNoteTypeIdAsync(Guid tenantId, Guid noteTypeId, CancellationToken ct = default)
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey
                    && e.NoteTypeId == noteTypeId
                    && !e.IsSystem
                    && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                return entity;
            }

            return null;
        }

        private static void SyncEntityIdentityFromKeys(NoteTypeProfile entity)
        {
            if (entity.Id == Guid.Empty && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
