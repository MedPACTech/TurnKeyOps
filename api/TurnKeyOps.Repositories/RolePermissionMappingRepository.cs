using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class RolePermissionMappingRepository : AzureTablesRepositoryBase<RolePermissionMapping>, IRolePermissionMappingRepository
    {
        public const string SystemPartitionKey = "ROLEPERM|SYSTEM";

        private readonly IAzureTablesRepositoryStore<RolePermissionMapping> _azureStore;

        public RolePermissionMappingRepository(
            IAzureTablesRepositoryStore<RolePermissionMapping> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<IReadOnlyList<RolePermissionMapping>> GetSystemMappingsAsync(CancellationToken ct = default)
        {
            var results = new List<RolePermissionMapping>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == SystemPartitionKey && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<RolePermissionMapping>> GetTenantMappingsAsync(Guid tenantId, CancellationToken ct = default)
        {
            var partitionKey = $"ROLEPERM|TENANT={tenantId:N}";
            var results = new List<RolePermissionMapping>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                results.Add(entity);
            }

            return results;
        }

        public async Task<IReadOnlyList<RolePermissionMapping>> GetMappingsForRoleAsync(Guid? tenantId, Guid roleId, CancellationToken ct = default)
        {
            var partitionKey = tenantId.HasValue ? $"ROLEPERM|TENANT={tenantId.Value:N}" : SystemPartitionKey;
            var results = new List<RolePermissionMapping>();
            await foreach (var entity in _azureStore.QueryAsync(
                x => x.PartitionKey == partitionKey && x.RoleId == roleId && !x.IsDeleted,
                ct,
                "IsDeleted"))
            {
                results.Add(entity);
            }

            return results;
        }
    }
}
