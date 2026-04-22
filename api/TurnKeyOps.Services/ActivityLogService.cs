using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Services.Mappers;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Lib.Utils;
using DocumentFormat.OpenXml.Spreadsheet;

namespace MedInsights.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IActivityLogRepository _logRepo;
        private readonly IActivityEntryRepository _entryRepo;
        private readonly IUserContext _userContext;

        // If you already have a central SafeKey helper, use that instead.
        private static string SafeKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "unknown";

            var cleaned = new string(raw
                .Trim()
                .ToLowerInvariant()
                .Select(ch => (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-') ? ch : '_')
                .ToArray());

            while (cleaned.Contains("__"))
                cleaned = cleaned.Replace("__", "_");

            return cleaned.Trim('_');
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ActivityLogService(
            IActivityLogRepository logRepo,
            IActivityEntryRepository entryRepo,
            IUserContext userContext)
        {
            _logRepo = logRepo;
            _entryRepo = entryRepo;
            _userContext = userContext;
        }

        private string PartitionKeyForUser(Guid tenantId, DateTime entryDate) =>
            RepositoryKeyHelper.BuildPartitionKey(tenantId, entryDate);
            
        private string RowKeyForUserItem(DateTime entryDate, Guid userId) =>
            RepositoryKeyHelper.BuildRowKey(entryDate, userId);

        public async Task<IEnumerable<ActivityLogItemDto>> GetEntryForUserByDateAsync(
            DateTime entryDate,
            CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForUser(_userContext.TenantId, entryDate);
            var ed = entryDate;
            var user = _userContext.UserId;

            var entities = await _entryRepo.GetEntryForUserByDateAsync(pk, ed, user, ct);

            return entities.Select(e => ActivityEntryMapper.ToDto(e));
        }


        public async Task<ActivityLogDto?> GetAsync(
            Guid tenantId,
            Guid userId,
            DateTime entryDate,
            CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = await _logRepo.GetByContextAsync(
                _userContext.TenantId,
                _userContext.UserId,
                entryDate,
                ct);

            return entity == null ? null : ActivityLogMapper.ToDto(entity);
        }

        public async Task<IReadOnlyList<ActivityReadDto>> GetEntriesForMonthAsync(
        DateTime month,
        CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            // Optional sanity check on year if you want:
            // if (year < 2000 || year > DateTime.UtcNow.Year + 1)
            //     throw new ArgumentOutOfRangeException(nameof(year), "Year is out of expected range.");

            var tenantId = _userContext.TenantId;
            
            var pk = RepositoryKeyHelper.BuildPartitionKey(tenantId, month);

            // Repository encapsulates the Azure Table partition logic:
            //   PartitionKey = TENANT|{tenantId}|MONTH|{yyyyMM}
            var entities = await _entryRepo.GetForMonthAsync(pk, ct);

            if (entities == null || entities.Count == 0)
                return Array.Empty<ActivityReadDto>();

            var dtos = entities
                .Select(ActivityReadMapper.ToReadDto)
                .OrderBy(x => x.EntryDate)
                .ThenBy(x => x.Type)
                .ThenBy(x => x.Key)
                .ToList();

            return dtos;
        }

        public async Task<ActivityLogDto> UpsertAsync(
            ActivityLogUpsertDto dto,
            CancellationToken ct = default)
        {

            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var now = DateTime.UtcNow;

            // sanitize incoming item keys
            foreach (var item in dto.Items)
                item.Key = SafeKey(item.Key);

            // ---------------------------------------------
            // 1) ALWAYS write an ActivityLog (event history)
            // ---------------------------------------------
            var pk = RepositoryKeyHelper.BuildPartitionKey(_userContext.TenantId, dto.EntryDate);
            
            var eventRk = $"DATE|{dto.EntryDate:yyyyMMdd}"
                        + (dto.FacilityId.HasValue ? $"|FAC|{dto.FacilityId}" : "")
                        + $"|USER|{_userContext.UserId}|EVENT|{Guid.NewGuid()}";

            var logId = Guid.NewGuid();
            var logEntity = new ActivityLog
            {
                Id = logId,
                LogId = logId,
                PartitionKey = pk,
                RowKey = eventRk,                        // <-- unique per post
                EntryDate = DateTime.SpecifyKind(dto.EntryDate.Date, DateTimeKind.Utc),
                TenantId = _userContext.TenantId,
                UserId = _userContext.UserId,
                FacilityId = dto.FacilityId,
                ItemsJson = JsonSerializer.Serialize(dto.Items, JsonOptions),
                Narrative = dto.Narrative,
                EnteredBy = _userContext.UserId,
                EnteredAt = now,
                UpdatedAt = now,
                IsDeleted = false
            };

            await _logRepo.SaveAsync(logEntity, ct);

            // ---------------------------------------------------------
            // 2) Rollup into ActivityEntries (deterministic per item)
            // ---------------------------------------------------------
            var entryEntities = new List<ActivityItems>();

            foreach (var item in dto.Items)
            {
                var entryRk =
                    dto.FacilityId.HasValue
                    ? $"DATE|{dto.EntryDate:yyyyMMdd}|FAC|{dto.FacilityId}|USER|{_userContext.UserId}|ITEM|{item.Key}"
                    : $"DATE|{dto.EntryDate:yyyyMMdd}|USER|{_userContext.UserId}|ITEM|{item.Key}";

                var existing = await _entryRepo.GetAsync(pk, entryRk, ct);

                var newTotal = (existing?.NumericValue ?? 0) + item.Value;

                entryEntities.Add(new ActivityItems
                {
                    Id = existing?.Id ?? Guid.NewGuid(),
                    PartitionKey = pk,
                    RowKey = entryRk,

                    EntryDate = logEntity.EntryDate,
                    TenantId = _userContext.TenantId,
                    UserId = _userContext.UserId,
                    FacilityId = dto.FacilityId,

                    UserFirstName = _userContext.FirstName,
                    UserLastName = _userContext.LastName,

                    ItemKey = item.Key,
                    ItemType = item.Type,
                    NumericValue = newTotal,
                    Unit = item.Unit,

                    EnteredBy = _userContext.UserId,
                    EnteredAt = existing?.EnteredAt ?? now,
                    UpdatedAt = now,
                    IsDeleted = false
                });
            }

            await _entryRepo.UpsertBatchAsync(entryEntities, ct);

            return ActivityLogMapper.ToDto(logEntity);
        }
    }
}
