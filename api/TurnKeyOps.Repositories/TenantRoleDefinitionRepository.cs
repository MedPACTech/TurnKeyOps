using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class TenantRoleDefinitionRepository : AzureTablesRepositoryBase<TenantRoleDefinition>, ITenantRoleDefinitionRepository
    {
        public const string SystemPartitionKey = "ROLEDEF|SYSTEM";

        private readonly IAzureTablesRepositoryStore<TenantRoleDefinition> _azureStore;

        public TenantRoleDefinitionRepository(
            IAzureTablesRepositoryStore<TenantRoleDefinition> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<TenantRoleDefinition?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<TenantRoleDefinition?> GetSystemByKeyAsync(string key, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == SystemPartitionKey && x.Key == key && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                return entity;
            }

            return null;
        }

        public async Task<TenantRoleDefinition?> GetTenantByKeyAsync(Guid tenantId, string key, CancellationToken ct = default)
        {
            var partitionKey = $"ROLEDEF|TENANT={tenantId:N}";
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && x.Key == key && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                return entity;
            }

            return null;
        }

        public async Task<IReadOnlyList<TenantRoleDefinition>> GetSystemRolesAsync(CancellationToken ct = default)
        {
            var results = new List<TenantRoleDefinition>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == SystemPartitionKey && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                results.Add(entity);
            }

            return results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<IReadOnlyList<TenantRoleDefinition>> GetTenantRolesAsync(Guid tenantId, CancellationToken ct = default)
        {
            var partitionKey = $"ROLEDEF|TENANT={tenantId:N}";
            var results = new List<TenantRoleDefinition>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                results.Add(entity);
            }

            return results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
