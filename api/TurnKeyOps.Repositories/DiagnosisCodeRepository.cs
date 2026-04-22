using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class DiagnosisCodeRepository : AzureTablesRepositoryBase<DiagnosisCode>, IDiagnosisCodeRepository
    {
        private readonly IAzureTablesRepositoryStore<DiagnosisCode> _azureStore;

        public DiagnosisCodeRepository(
            IAzureTablesRepositoryStore<DiagnosisCode> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<DiagnosisCode>> GetAllAsync(CancellationToken ct = default)
        {
            var results = new List<DiagnosisCode>();
            await foreach (var entity in _azureStore.QueryAsync(_ => true, ct))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        private static void SyncEntityIdentityFromKeys(DiagnosisCode entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
