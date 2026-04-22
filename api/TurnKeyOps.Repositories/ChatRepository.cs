using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class ChatRepository : AzureTablesRepositoryBase<Chat>, IChatRepository
    {
        private readonly IAzureTablesRepositoryStore<Chat> _azureStore;

        public ChatRepository(
            IAzureTablesRepositoryStore<Chat> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<Chat?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;
            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<Chat?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default)
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

        public async Task<List<Chat>> GetChatsByUserAsync(string partitionKey, CancellationToken ct)
        {
            var results = new List<Chat>();
            await foreach (var chat in _azureStore.QueryAsync(x => x.PartitionKey == partitionKey && !x.IsDeleted, ct, "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(chat);
                results.Add(chat);
            }

            return results.OrderByDescending(c => c.DateChatUpdated).ToList();
        }

        public async Task<List<Chat>> GetChatsByUserAndPatientIdAsync(string partitionKey, Guid patientId, CancellationToken ct)
        {
            var results = new List<Chat>();
            await foreach (var chat in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && x.PatientId == patientId && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(chat);
                results.Add(chat);
            }

            return results.OrderByDescending(c => c.DateChatUpdated).ToList();
        }

        public async Task<List<Chat>> GetChatsByTenantAndPatientIdAsync(string tenantPartitionPrefix, Guid patientId, CancellationToken ct)
        {
            var results = new List<Chat>();
            await foreach (var chat in _azureStore.QueryAsync(x => x.PatientId == patientId && !x.IsDeleted, ct, "IsDeleted"))
            {
                if (!string.IsNullOrWhiteSpace(chat.PartitionKey) && chat.PartitionKey.StartsWith($"TENANT={tenantPartitionPrefix}", StringComparison.Ordinal))
                {
                    SyncEntityIdentityFromKeys(chat);
                    results.Add(chat);
                }
            }

            return results.OrderByDescending(c => c.DateChatUpdated).ToList();
        }

        private static void SyncEntityIdentityFromKeys(Chat entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
