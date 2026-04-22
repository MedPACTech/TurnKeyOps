using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class WebhookEventRepository : AzureTablesRepositoryBase<WebhookEvent>, IWebhookEventRepository
    {
        public WebhookEventRepository(
            IAzureTablesRepositoryStore<WebhookEvent> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
        }

        public async Task<WebhookEvent?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
    }
}
