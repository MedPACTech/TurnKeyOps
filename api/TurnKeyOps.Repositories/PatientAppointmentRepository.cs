using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Core;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Models;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MedInsights.Repositories
{
    public sealed class PatientAppointmentRepository : AzureTablesRepositoryBase<PatientAppointment>, IPatientAppointmentRepository
    {
        private readonly IAzureTablesRepositoryStore<PatientAppointment> _azureStore;

        public PatientAppointmentRepository(
            IAzureTablesRepositoryStore<PatientAppointment> store,
            IMemoryCache memoryCache,
            ITenantContext tenantContext,
            IOptions<RepositoryOptions> repositoryOptions)
            : base(store, memoryCache, tenantContext, repositoryOptions.Value)
        {
            _azureStore = store;
        }

        public async Task<PatientAppointment?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false)
        {
            var entity = await GetByKeysAsync(partitionKey, rowKey, ct, includeDeleted: includeDeleted);
            if (entity is null) return null;

            SyncEntityIdentityFromKeys(entity);
            return entity;
        }

        public async Task<IReadOnlyList<PatientAppointment>> SearchAsync(
            AppointmentSearchRepositoryFilter filter,
            CancellationToken cancellationToken = default)
        {
            if (filter is null) throw new ArgumentNullException(nameof(filter));
            if (string.IsNullOrWhiteSpace(filter.TenantPartitionKey))
                throw new ArgumentException("TenantPartitionKey is required.", nameof(filter));

            var all = new List<PatientAppointment>();
            await foreach (var entity in _azureStore.QueryAsync(
                e => e.PartitionKey == filter.TenantPartitionKey,
                cancellationToken,
                "IsDeleted"))
            {
                SyncEntityIdentityFromKeys(entity);
                all.Add(entity);
            }

            IEnumerable<PatientAppointment> filtered = all;

            if (!string.IsNullOrEmpty(filter.PatientRowKey))
                filtered = filtered.Where(e => e.PatientId == filter.PatientRowKey);

            if (!string.IsNullOrEmpty(filter.ProviderRowKey))
                filtered = filtered.Where(e => e.UserId == filter.ProviderRowKey);

            if (filter.FromUtc.HasValue)
            {
                var from = EnsureUtc(filter.FromUtc.Value);
                filtered = filtered.Where(e => e.AppointmentStartTime >= from);
            }

            if (filter.ToExclusiveUtc.HasValue)
            {
                var toEx = EnsureUtc(filter.ToExclusiveUtc.Value);
                filtered = filtered.Where(e => e.AppointmentStartTime < toEx);
            }

            bool desc = string.Equals(filter.Order, "desc", StringComparison.OrdinalIgnoreCase);
            IEnumerable<PatientAppointment> ordered = (filter.Sort ?? "start").ToLowerInvariant() switch
            {
                "created" => desc ? filtered.OrderByDescending(e => e.DateCreated) : filtered.OrderBy(e => e.DateCreated),
                "updated" => desc ? filtered.OrderByDescending(e => e.DateUpdated) : filtered.OrderBy(e => e.DateUpdated),
                _ => desc ? filtered.OrderByDescending(e => e.AppointmentStartTime) : filtered.OrderBy(e => e.AppointmentStartTime),
            };

            var page = filter.Page <= 0 ? 1 : filter.Page;
            var size = (filter.PageSize <= 0 || filter.PageSize > 500) ? 50 : filter.PageSize;

            return ordered.Skip((page - 1) * size).Take(size).ToList();
        }

        private static DateTime EnsureUtc(DateTime dt)
            => dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            };

        private static void SyncEntityIdentityFromKeys(PatientAppointment entity)
        {
            if (entity.Id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var id))
                entity.Id = id;
        }
    }
}
