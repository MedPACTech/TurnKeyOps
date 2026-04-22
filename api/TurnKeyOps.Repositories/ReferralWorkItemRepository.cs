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
    public sealed class ReferralWorkItemRepository : AzureTablesRepositoryBase<ReferralWorkItem>, IReferralWorkItemRepository
    {
        private readonly IAzureTablesRepositoryStore<ReferralWorkItem> _azureStore;

        public ReferralWorkItemRepository(
            IAzureTablesRepositoryStore<ReferralWorkItem> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<ReferralWorkItem?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(
                EntityKeyPolicy.TenantPartition(tenantId),
                EntityKeyPolicy.Row(id),
                ct,
                includeDeleted: includeDeleted);

            if (entity is null)
                return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IReadOnlyList<ReferralWorkItem>> GetByTenantAsync(
            Guid tenantId,
            Guid? patientId = null,
            Guid? encounterId = null,
            string? status = null,
            string? search = null,
            CancellationToken ct = default)
        {
            var results = new List<ReferralWorkItem>();
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == partitionKey && !e.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);

                if (patientId.HasValue && entity.PatientId != patientId.Value)
                    continue;

                if (encounterId.HasValue && entity.EncounterId != encounterId.Value)
                    continue;

                if (!string.IsNullOrWhiteSpace(normalizedStatus)
                    && !string.Equals(entity.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!MatchesSearch(entity, normalizedSearch))
                    continue;

                results.Add(entity);
            }

            return results
                .OrderByDescending(x => x.DateUpdated)
                .ThenBy(x => x.PatientName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool MatchesSearch(ReferralWorkItem entity, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            return Contains(entity.PatientName, search)
                || Contains(entity.Mrn, search)
                || Contains(entity.CaseTitle, search)
                || Contains(entity.ReferralSource, search)
                || Contains(entity.Assignee, search)
                || Contains(entity.NextAction, search);
        }

        private static bool Contains(string? value, string search)
            => !string.IsNullOrWhiteSpace(value)
                && value.Contains(search, StringComparison.OrdinalIgnoreCase);

        private static void SyncEntityIdentityFromKeys(ReferralWorkItem entity)
        {
            if (entity.Id == Guid.Empty
                && !string.IsNullOrWhiteSpace(entity.RowKey)
                && Guid.TryParse(entity.RowKey, out var id))
            {
                entity.Id = id;
            }
        }
    }
}
