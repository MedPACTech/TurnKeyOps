using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class DocumentRepository : AzureTablesRepositoryBase<Document>, IDocumentRepository
    {
        private readonly IAzureTablesRepositoryStore<Document> _azureStore;

        public DocumentRepository(
            IAzureTablesRepositoryStore<Document> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<Document?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IEnumerable<Document>> GetDocumentsByUserIdAsync(Guid userId, CancellationToken ct)
        {
            var results = new List<Document>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.UserId == userId && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<IEnumerable<Document>> GetDocumentsByChatIdAsync(Guid chatId, CancellationToken ct)
        {
            var results = new List<Document>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.ChatId == chatId && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        public async Task<IEnumerable<Document>> GetDocumentsByIdsAsync(IList<Guid> documentIds, CancellationToken ct)
        {
            if (documentIds is null || documentIds.Count == 0)
                return Array.Empty<Document>();

            var idSet = documentIds.ToHashSet();
            var results = new List<Document>();
            await foreach (var entity in _azureStore.QueryAsync(e => !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                if (idSet.Contains(entity.Id))
                    results.Add(entity);
            }

            return results;
        }

        public async Task<IEnumerable<Document>> GetDocumentsByPatientIdAsync(Guid patientId, CancellationToken ct)
        {
            var results = new List<Document>();
            await foreach (var entity in _azureStore.QueryAsync(e => e.PatientId == patientId && !e.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        private static void SyncEntityIdentityFromKeys(Document entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
