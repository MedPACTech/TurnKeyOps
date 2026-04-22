using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class PatientReferralActivityRepository
        : AzureTablesRepositoryBase<PatientReferralActivity>, IPatientReferralActivityRepository
    {
        private readonly IAzureTablesRepositoryStore<PatientReferralActivity> _azureStore;

        public PatientReferralActivityRepository(
            IAzureTablesRepositoryStore<PatientReferralActivity> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<PatientReferralActivity>> GetByReferralAsync(Guid tenantId, Guid patientReferralId, CancellationToken ct = default)
        {
            var partitionKey = PartitionKeyForReferral(tenantId, patientReferralId);
            var results = new List<PatientReferralActivity>();

            await foreach (var entity in _azureStore.QueryAsync(x => x.PartitionKey == partitionKey && !x.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.RowKey, StringComparer.Ordinal)
                .ToList();
        }

        public static string PartitionKeyForReferral(Guid? tenantId, Guid patientReferralId)
            => tenantId.HasValue ? $"TENANT={tenantId.Value:N}|PATIENTREFERRAL={patientReferralId:N}" : string.Empty;

        public static string RowKeyFor(DateTime createdAtUtc, Guid id)
            => $"{createdAtUtc:yyyyMMdd'T'HHmmssfffffff'Z'}|{id:N}";

        private static void SyncEntityIdentityFromKeys(PatientReferralActivity entity)
        {
            if (entity.Id != Guid.Empty || string.IsNullOrWhiteSpace(entity.RowKey))
                return;

            var idSegment = entity.RowKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (!string.IsNullOrWhiteSpace(idSegment) && Guid.TryParse(idSegment, out var id))
                entity.Id = id;
        }
    }
}
