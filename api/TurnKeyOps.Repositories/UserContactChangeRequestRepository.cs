using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class UserContactChangeRequestRepository : AzureTablesRepositoryBase<UserContactChangeRequest>, IUserContactChangeRequestRepository
    {
        private readonly IAzureTablesRepositoryStore<UserContactChangeRequest> _azureStore;

        public UserContactChangeRequestRepository(
            IAzureTablesRepositoryStore<UserContactChangeRequest> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<UserContactChangeRequest?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<UserContactChangeRequest?> GetLatestPendingAsync(Guid userId, string channel, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.PartitionKey == $"USER={userId:N}"
                                    && e.Channel == channel
                                    && e.Status == "pending"
                                    && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                return entity;
            }

            return null;
        }
    }
}
