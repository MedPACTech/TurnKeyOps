using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class CaptureDraftNoteRepository : AzureTablesRepositoryBase<CaptureDraftNote>, ICaptureDraftNoteRepository
    {
        private readonly IAzureTablesRepositoryStore<CaptureDraftNote> _azureStore;

        public CaptureDraftNoteRepository(
            IAzureTablesRepositoryStore<CaptureDraftNote> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<(IEnumerable<CaptureDraftNote> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
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

        public async Task<IReadOnlyList<CaptureDraftNote>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default)
        {
            var results = new List<CaptureDraftNote>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PartitionKey == partitionKey && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<CaptureDraftNote>> GetByPartitionAndPatientAsync(string partitionKey, string patientRowKey, CancellationToken ct = default)
        {
            var results = new List<CaptureDraftNote>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey && e.PatientId == patientRowKey && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<CaptureDraftNote?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        private static void SyncEntityIdentityFromKeys(CaptureDraftNote entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
