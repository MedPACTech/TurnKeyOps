using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public class ChatMessageRepository : AzureTablesRepositoryBase<ChatMessage>, IChatMessageRepository
    {
        private readonly IAzureTablesRepositoryStore<ChatMessage> _azureStore;

        public ChatMessageRepository(
            IAzureTablesRepositoryStore<ChatMessage> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<List<ChatMessage>> GetMessagesByChatAsync(string partitionKey, Guid chatId, int limit, CancellationToken ct = default)
        {
            var prefix = RepositoryKeyHelper.GetOrderedRowKeyPrefix(chatId);

            var results = new List<ChatMessage>();
            await foreach (var entity in _azureStore.QueryAsync(x => x.PartitionKey == partitionKey && !x.IsDeleted, ct, "IsDeleted"))
            {
                if (!string.IsNullOrWhiteSpace(entity.RowKey) && entity.RowKey.StartsWith(prefix, StringComparison.Ordinal))
                {
                    SyncEntityIdentity(entity);
                    results.Add(entity);
                }
            }

            results = results.OrderBy(m => m.ChatTimestamp).ToList();

            if (limit > 0 && results.Count > limit)
                return results.Skip(results.Count - limit).ToList();

            return results;
        }

        public async Task DeleteMessagesByChatAsync(string partitionKey, Guid chatId, CancellationToken ct = default)
        {
            var messages = await GetMessagesByChatAsync(partitionKey, chatId, 0, ct);
            foreach (var message in messages)
            {
                message.IsDeleted = true;
                await SaveAsync(message, ct);
            }
        }

        private static void SyncEntityIdentity(ChatMessage entity)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = entity.MessageId;
        }
    }
}
