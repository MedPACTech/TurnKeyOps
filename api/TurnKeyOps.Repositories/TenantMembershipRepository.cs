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
    public sealed class TenantMembershipRepository : AzureTablesRepositoryBase<TenantMembership>, ITenantMembershipRepository
    {
        private readonly IAzureTablesRepositoryStore<TenantMembership> _azureStore;

        public TenantMembershipRepository(
            IAzureTablesRepositoryStore<TenantMembership> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<TenantMembership?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
            => await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);

        public async Task<TenantMembership?> GetByUserIdAsync(string partitionKey, Guid userId, CancellationToken ct = default)
        {
            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.PartitionKey == partitionKey && e.UserId == userId && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                return entity;
            }

            return null;
        }

        public async Task<IReadOnlyList<TenantMembership>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            var results = new List<TenantMembership>();

            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.UserId == userId && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                results.Add(entity);
            }

            return results;
        }

        public async Task<(IEnumerable<TenantMembership> Results, string? ContinuationToken)> GetByPartitionPagedAsync(
            string partitionKey,
            int pageSize,
            string? continuationToken = null,
            CancellationToken ct = default)
            => await _azureStore.GetByPartitionPagedAsync(partitionKey, pageSize, continuationToken, ct, "IsDeleted");

        public async Task<IReadOnlyList<TenantMembership>> GetActiveAssignedByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var results = new List<TenantMembership>();

            await foreach (var entity in _azureStore.QueryAsync(
                               e => e.PartitionKey == partitionKey && !e.IsDeleted,
                               ct,
                               "IsDeleted"))
            {
                if (string.Equals(entity.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entity.SeatStatus, "Assigned", StringComparison.OrdinalIgnoreCase)
                    && entity.UserId != Guid.Empty)
                {
                    results.Add(entity);
                }
            }

            return results;
        }

        public async Task<IReadOnlyList<Guid>> GetTenantIdsAsync(CancellationToken ct = default)
        {
            var ids = new HashSet<Guid>();

            await foreach (var entity in _azureStore.QueryAsync(e => !e.IsDeleted, ct, "IsDeleted"))
            {
                if (entity.TenantId != Guid.Empty)
                {
                    ids.Add(entity.TenantId);
                    continue;
                }

                if (TryGetTenantId(entity.PartitionKey, out var tenantId))
                    ids.Add(tenantId);
            }

            return ids.ToList();
        }

        private static bool TryGetTenantId(string? partitionKey, out Guid tenantId)
        {
            tenantId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(partitionKey))
                return false;

            if (Guid.TryParse(partitionKey, out tenantId) && tenantId != Guid.Empty)
                return true;

            var segments = partitionKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (!segment.StartsWith("TENANT=", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = segment["TENANT=".Length..];
                if (Guid.TryParse(value, out tenantId) && tenantId != Guid.Empty)
                    return true;
            }

            return false;
        }
    }
}
