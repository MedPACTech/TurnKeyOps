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
    public sealed class PatientClinicalSummaryCacheRepository : AzureTablesRepositoryBase<PatientClinicalSummaryCache>, IPatientClinicalSummaryCacheRepository
    {
        public PatientClinicalSummaryCacheRepository(
            IAzureTablesRepositoryStore<PatientClinicalSummaryCache> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
        }

        public async Task<PatientClinicalSummaryCache?> GetAsync(
            Guid tenantId,
            Guid patientId,
            CancellationToken ct = default,
            bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(
                EntityKeyPolicy.TenantPatientPartition(tenantId, patientId),
                EntityKeyPolicy.Row(patientId),
                ct,
                includeDeleted: includeDeleted);

            if (entity is null)
                return null;

            if (entity.Id == Guid.Empty)
                entity.Id = patientId;

            if (entity.PatientId == Guid.Empty)
                entity.PatientId = patientId;

            if (entity.TenantId == Guid.Empty)
                entity.TenantId = tenantId;

            return entity;
        }
    }
}
