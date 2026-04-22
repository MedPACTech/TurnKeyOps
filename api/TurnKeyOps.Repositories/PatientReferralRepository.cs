using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class PatientReferralRepository
        : AzureTablesRepositoryBase<PatientReferral>, IPatientReferralRepository
    {
        private readonly IAzureTablesRepositoryStore<PatientReferral> _azureStore;

        public PatientReferralRepository(
            IAzureTablesRepositoryStore<PatientReferral> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<PatientReferral>> GetByPatientAsync(string partitionKey)
        {
            var results = new List<PatientReferral>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && !x.IsDeleted,
                softDeleteProperty: "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<PatientReferral>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            var results = new List<PatientReferral>();

            await foreach (var entity in _azureStore.QueryAsync(x => !x.IsDeleted, ct, "IsDeleted"))
            {
                if (!BelongsToTenant(entity.PartitionKey, tenantId))
                {
                    continue;
                }

                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results
                .OrderByDescending(x => x.DateUpdated)
                .ThenByDescending(x => x.DateCreated)
                .ToList();
        }

        public async Task<PatientReferral?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<PatientReferral?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.RowKey == rowKey && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                return entity;
            }

            return null;
        }

        public async Task<PatientReferral?> GetByCaptureDraftNoteIdAsync(Guid tenantId, Guid captureDraftNoteId, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.CaptureDraftNoteId == captureDraftNoteId && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                if (!BelongsToTenant(entity.PartitionKey, tenantId))
                {
                    continue;
                }

                SyncEntityIdentityFromKeys(entity);
                return entity;
            }

            return null;
        }

        private static void SyncEntityIdentityFromKeys(PatientReferral entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
            {
                entity.Id = id;
            }
        }

        private static bool BelongsToTenant(string? partitionKey, Guid tenantId)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                return false;

            var segments = partitionKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
                return false;

            var tenantSegment = segments[0];
            if (!tenantSegment.StartsWith("TENANT=", StringComparison.OrdinalIgnoreCase))
                return false;

            var rawTenant = tenantSegment["TENANT=".Length..];
            return Guid.TryParse(rawTenant, out var parsedTenant) && parsedTenant == tenantId;
        }
    }
}

