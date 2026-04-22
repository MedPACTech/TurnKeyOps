using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class PatientMilitaryFirstResponderRepository
        : AzureTablesRepositoryBase<PatientMilitaryFirstResponder>, IPatientMilitaryFirstResponderRepository
    {
        private readonly IAzureTablesRepositoryStore<PatientMilitaryFirstResponder> _azureStore;

        public PatientMilitaryFirstResponderRepository(
            IAzureTablesRepositoryStore<PatientMilitaryFirstResponder> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<PatientMilitaryFirstResponder>> GetByPatientAsync(string partitionKey)
        {
            var results = new List<PatientMilitaryFirstResponder>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && !x.IsDeleted,
                softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<PatientMilitaryFirstResponder?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        private static void SyncEntityIdentityFromKeys(PatientMilitaryFirstResponder entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
