using System.Globalization;
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
    public class PatientRepository : AzureTablesRepositoryBase<Patient>, IPatientRepository
    {
        private readonly IAzureTablesRepositoryStore<Patient> _azureStore;

        public PatientRepository(
            IAzureTablesRepositoryStore<Patient> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<Patient?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity == null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<(IEnumerable<Patient> Results, string? ContinuationToken)> GetByPartitionPagedAsync(string partitionKey, int pageSize, string? continuationToken = null, CancellationToken ct = default)
        {
            var (results, nextToken) = await _azureStore.GetByPartitionPagedAsync(partitionKey, pageSize, continuationToken, ct, "IsDeleted");
            foreach (var item in results)
                SyncEntityIdentityFromKeys(item);

            return (results, nextToken);
        }

        public async Task<IReadOnlyList<Patient>> GetByPartitionAsync(string partitionKey, CancellationToken ct = default)
        {
            var results = new List<Patient>();

            await foreach (var entity in _azureStore.QueryAsync(
                p => p.PartitionKey == partitionKey && !p.IsDeleted,
                ct,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                results.Add(entity);
            }

            return results;
        }

        private static DateTime? TryParseFlexibleDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string[] formats = new[]
            {
                "MMMM d", "MMMM dd", "MMM d", "MMM dd",
                "M/d", "MM/dd", "d MMMM", "d MMM",
            };

            if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;

            if (DateTime.TryParse(raw, out parsed))
                return parsed;

            return null;
        }

        private static DateTime? ExtractDobFromTerms(List<string> terms, out List<string> remainingTerms)
        {
            remainingTerms = new List<string>(terms);

            for (int i = 0; i < terms.Count; i++)
            {
                for (int len = 1; len <= 3 && i + len <= terms.Count; len++)
                {
                    var candidate = string.Join(" ", terms.Skip(i).Take(len));
                    var parsed = TryParseFlexibleDate(candidate);
                    if (parsed != null)
                    {
                        remainingTerms.RemoveRange(i, len);
                        return parsed;
                    }
                }
            }

            return null;
        }

        public async Task<List<Patient>> SearchPatientAsync(string tenantId, string[] rawTerms)
        {
            // TODO(cleanup): Remove dual key-format fallback after runtime/data migration standardizes GUID key format app-wide.
            var candidatePartitionKeys = GetCandidatePartitionKeys(tenantId);
            var all = new List<Patient>();
            var seenRowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var partitionKey in candidatePartitionKeys)
            {
                await foreach (var p in _azureStore.QueryAsync(p => p.PartitionKey == partitionKey))
                {
                    // Keep compatibility with rows written before soft-delete persistence was standardized.
                    if (p.IsDeleted) continue;
                    if (!seenRowKeys.Add(p.RowKey)) continue;

                    SyncEntityIdentityFromKeys(p);
                    all.Add(p);
                }
            }

            if (rawTerms == null || rawTerms.Length == 0) return all;

            var normalized = rawTerms
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToList();

            var dob = ExtractDobFromTerms(normalized, out var remainingTerms);

            return all.Where(p =>
            {
                var first = p.FirstName?.ToLowerInvariant() ?? string.Empty;
                var last = p.LastName?.ToLowerInvariant() ?? string.Empty;

                var nameMatch = remainingTerms.All(t => first.Contains(t) || last.Contains(t));
                var dobMatch = dob == null ||
                               (p.DateOfBirth.Month == dob.Value.Month &&
                                p.DateOfBirth.Day == dob.Value.Day);

                return nameMatch && dobMatch;
            }).ToList();
        }

        private static List<string> GetCandidatePartitionKeys(string tenantPartitionKey)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(tenantPartitionKey))
                keys.Add(tenantPartitionKey);

            if (Guid.TryParse(tenantPartitionKey, out var tenantId))
            {
                keys.Add(tenantId.ToString("D"));
                keys.Add(tenantId.ToString("N"));
            }

            return keys.ToList();
        }

        private static void SyncEntityIdentityFromKeys(Patient entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
