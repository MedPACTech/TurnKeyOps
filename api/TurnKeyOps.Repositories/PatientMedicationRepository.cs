using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class PatientMedicationRepository : AzureTablesRepositoryBase<PatientMedication>, IPatientMedicationRepository
    {
        private readonly IAzureTablesRepositoryStore<PatientMedication> _azureStore;

        public PatientMedicationRepository(
            IAzureTablesRepositoryStore<PatientMedication> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<PatientMedication?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IReadOnlyList<PatientMedication>> GetByPatientAsync(string partitionKey, Guid patientId)
        {
            var results = new List<PatientMedication>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && x.PatientId == patientId && !x.IsDeleted,
                softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<PatientMedication>> GetByProviderAsync(string partitionKey, Guid providerId)
        {
            var results = new List<PatientMedication>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && x.ProviderId == providerId && !x.IsDeleted,
                softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        private static void SyncEntityIdentityFromKeys(PatientMedication entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
