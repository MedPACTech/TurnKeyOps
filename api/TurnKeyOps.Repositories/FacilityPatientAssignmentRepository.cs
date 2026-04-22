using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class FacilityPatientAssignmentRepository : AzureTablesRepositoryBase<FacilityPatientAssignment>, IFacilityPatientAssignmentRepository
    {
        private readonly IAzureTablesRepositoryStore<FacilityPatientAssignment> _azureStore;

        public FacilityPatientAssignmentRepository(
            IAzureTablesRepositoryStore<FacilityPatientAssignment> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<FacilityPatientAssignment?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IReadOnlyList<FacilityPatientAssignment>> GetByFacilityAsync(string partitionKey, CancellationToken ct = default)
        {
            var results = new List<FacilityPatientAssignment>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<FacilityPatientAssignment?> GetActiveByPatientAsync(string partitionKey, Guid patientId, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey
                    && e.PatientId == patientId
                    && !e.IsDeleted
                    && e.Status == "Admitted",
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                return entity;
            }

            return null;
        }

        private static void SyncEntityIdentityFromKeys(FacilityPatientAssignment entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey))
            {
                var lastSegment = entity.RowKey.Split('|', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (lastSegment is not null && Guid.TryParse(lastSegment, out var id))
                    entity.Id = id;
            }
        }
    }
}
