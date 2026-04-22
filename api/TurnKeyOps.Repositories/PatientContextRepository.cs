using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace MedInsights.Repositories
{
    public class PatientContextRepository : AzureTablesRepositoryBase<PatientContext>, IPatientContextRepository
    {
        private readonly IAzureTablesRepositoryStore<PatientContext> _azureStore;

        public PatientContextRepository(
            IAzureTablesRepositoryStore<PatientContext> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<PatientContext?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity == null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IEnumerable<PatientContext>> GetPatientsAsync(string partitionKey)
        {
            var results = new List<PatientContext>();
            foreach (var pk in GetPartitionKeyCandidates(partitionKey))
            {
                await foreach (var entity in _azureStore.QueryAsync(pc => pc.PartitionKey == pk))
                {
                    if (entity.IsDeleted) continue;
                    SyncEntityIdentityFromKeys(entity);
                    if (!results.Any(x => x.PartitionKey == entity.PartitionKey && x.RowKey == entity.RowKey))
                        results.Add(entity);
                }
            }
            return results;
        }

        public async Task<IEnumerable<PatientContext>> GetActivePatientAsync(string partitionKey)
        {
            var results = new List<PatientContext>();
            foreach (var pk in GetPartitionKeyCandidates(partitionKey))
            {
                await foreach (var entity in _azureStore.QueryAsync(pc => pc.PartitionKey == pk))
                {
                    if (entity.IsDeleted) continue;
                    SyncEntityIdentityFromKeys(entity);
                    if (!results.Any(x => x.PartitionKey == entity.PartitionKey && x.RowKey == entity.RowKey))
                        results.Add(entity);
                }
            }
            return results
                .OrderByDescending(x => x.DateActivated)
                .Take(1)
                .ToList();
        }

        public async Task<PatientContext?> GetByPatientIdAsync(string partitionKey, string patientId, CancellationToken ct = default)
        {
            var results = new List<PatientContext>();
            foreach (var pk in GetPartitionKeyCandidates(partitionKey))
            {
                foreach (var pid in GetGuidKeyCandidates(patientId))
                {
                    await foreach (var entity in _azureStore.QueryAsync(pc => pc.PartitionKey == pk && pc.PatientId == pid, ct))
                    {
                        if (entity.IsDeleted) continue;
                        SyncEntityIdentityFromKeys(entity);
                        if (!results.Any(x => x.PartitionKey == entity.PartitionKey && x.RowKey == entity.RowKey))
                            results.Add(entity);
                    }
                }
            }

            return results
                .OrderByDescending(x => x.DateActivated)
                .FirstOrDefault();
        }

        private static IEnumerable<string> GetPartitionKeyCandidates(string partitionKey)
        {
            if (string.IsNullOrWhiteSpace(partitionKey))
                return [partitionKey];

            var set = new HashSet<string>(StringComparer.Ordinal) { partitionKey };

            var match = Regex.Match(partitionKey, "^TENANT=(?<tenant>[^|]+)\\|USER=(?<user>[^|]+)$");
            if (!match.Success)
                return set;

            if (!Guid.TryParse(match.Groups["tenant"].Value, out var tenantId) ||
                !Guid.TryParse(match.Groups["user"].Value, out var userId))
                return set;

            set.Add($"TENANT={tenantId:N}|USER={userId:N}");
            set.Add($"TENANT={tenantId:D}|USER={userId:D}");
            return set;
        }

        private static IEnumerable<string> GetGuidKeyCandidates(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return [key];

            var set = new HashSet<string>(StringComparer.Ordinal) { key };
            if (!Guid.TryParse(key, out var value))
                return set;

            set.Add(value.ToString("N"));
            set.Add(value.ToString("D"));
            return set;
        }

        private static void SyncEntityIdentityFromKeys(PatientContext entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
