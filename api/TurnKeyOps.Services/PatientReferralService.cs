using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientReferralService : IPatientReferralService
    {
        private readonly IPatientReferralRepository _repository;
        private readonly IPatientReferralActivityService _activityService;
        private readonly IPatientService _patientService;
        private readonly IPatientContactService _patientContactService;
        private readonly IUserProfileService _userProfileService;
        private readonly IPatientClinicalSummaryService _patientClinicalSummaryService;
        private readonly IUserContext _userContext;

        public PatientReferralService(
            IPatientReferralRepository repository,
            IPatientReferralActivityService activityService,
            IPatientService patientService,
            IPatientContactService patientContactService,
            IUserProfileService userProfileService,
            IPatientClinicalSummaryService patientClinicalSummaryService,
            IUserContext userContext)
        {
            _repository = repository;
            _activityService = activityService;
            _patientService = patientService;
            _patientContactService = patientContactService;
            _userProfileService = userProfileService;
            _patientClinicalSummaryService = patientClinicalSummaryService;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientReferralDto?> GetAsync(Guid patientId, Guid referralId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(referralId);
            var item = await _repository.GetAsync(pk, rowKey);

            return item == null ? null : PatientReferralMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientReferralDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);

            return items.Select(PatientReferralMapper.ToDto);
        }

        public async Task<IReadOnlyList<PatientReferralQueueItemDto>> GetQueueAsync(
            Guid? patientId = null,
            string? status = null,
            string? search = null,
            CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var items = await _repository.GetByTenantAsync(_userContext.TenantId, ct);
            var filteredItems = items
                .Where(item => !patientId.HasValue || item.PatientId == patientId.Value)
                .Where(item => string.IsNullOrWhiteSpace(status) || string.Equals(NormalizeStatus(item), status.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var patients = await _patientService.GetByIdsAsync(filteredItems.Select(x => x.PatientId).Distinct());
            var contacts = await LoadPatientContactsAsync(filteredItems.Select(x => x.PatientId).Distinct(), ct);

            var queue = filteredItems
                .Select(item => ToQueueItem(item, patients, contacts))
                .Where(item => MatchesSearch(item, search))
                .OrderByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.PatientName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return queue;
        }

        public async Task<PatientReferralDto> RefreshCaseSummaryAsync(Guid patientId, Guid referralId, bool forceRefresh = false, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(referralId);
            var existing = await _repository.GetAsync(pk, rowKey, ct)
                ?? throw new KeyNotFoundException("Referral not found.");

            var summary = await _patientClinicalSummaryService.GenerateAsync(patientId, forceRefresh, ct);
            existing.CaseSummary = FirstNonEmpty(summary.ReferralCaseSummary, summary.Narrative, existing.CaseSummary, existing.ReferralBody);
            existing.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(existing, ct);
            await _activityService.AppendAsync(new CreatePatientReferralActivityDto
            {
                PatientReferralId = saved.Id,
                PatientId = saved.PatientId,
                ActivityType = PatientReferralActivityTypes.WorkflowUpdated,
                Title = "Case summary refreshed",
                Body = FirstNonEmpty(saved.CaseSummary, summary.MostRecentConcern, saved.ReferralReason, saved.ReferralBody)
            }, ct);

            return PatientReferralMapper.ToDto(saved);
        }

        public async Task<PatientReferralDto> AddAsync(PatientReferralDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientReferralMapper.ToEntity(dto);
            var creator = await ResolveCreatorAsync();
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.CreatedByUserId ??= creator.UserId;
            entity.CreatedByFirstName = FirstNonEmpty(entity.CreatedByFirstName, creator.FirstName);
            entity.CreatedByLastName = FirstNonEmpty(entity.CreatedByLastName, creator.LastName);
            if (entity.DateCreated == default) entity.DateCreated = DateTime.UtcNow;
            entity.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            await _activityService.AppendAsync(new CreatePatientReferralActivityDto
            {
                PatientReferralId = saved.Id,
                PatientId = saved.PatientId,
                ActivityType = PatientReferralActivityTypes.ReferralCreated,
                Title = "Referral created",
                Body = FirstNonEmpty(saved.ReferralReason, saved.CaseSummary, saved.ReferralBody, saved.NoteTitle),
                CreatedAtUtc = saved.DateCreated,
                CreatedByUserId = saved.CreatedByUserId,
                CreatedByName = BuildPersonName(saved.CreatedByFirstName, saved.CreatedByLastName)
            });
            if (saved.DateSent != default)
            {
                await _activityService.AppendAsync(new CreatePatientReferralActivityDto
                {
                    PatientReferralId = saved.Id,
                    PatientId = saved.PatientId,
                    ActivityType = PatientReferralActivityTypes.ReferralSent,
                    Title = "Referral sent",
                    Body = FirstNonEmpty(saved.SentTo, saved.ReferralReason, saved.CaseSummary, saved.ReferralBody),
                    CreatedAtUtc = saved.DateSent,
                    CreatedByUserId = saved.CreatedByUserId,
                    CreatedByName = BuildPersonName(saved.CreatedByFirstName, saved.CreatedByLastName)
                });
            }

            return PatientReferralMapper.ToDto(saved);
        }

        public async Task<PatientReferralDto> UpdateAsync(PatientReferralDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Referral not found.");

            var entity = PatientReferralMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            entity.DateCreated = existing.DateCreated == default ? entity.DateCreated : existing.DateCreated;
            entity.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            await AppendWorkflowActivitiesAsync(existing, saved);
            return PatientReferralMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid id)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var existing = await _repository.GetByRowKeyAsync(EntityKeyPolicy.Row(id), CancellationToken.None)
                ?? throw new KeyNotFoundException("Referral not found.");

            existing.IsDeleted = true;
            existing.DateUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(existing);
        }

        private async Task<Dictionary<Guid, string>> LoadPatientContactsAsync(IEnumerable<Guid> patientIds, CancellationToken ct)
        {
            var distinctPatientIds = patientIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var contacts = new Dictionary<Guid, string>();

            foreach (var id in distinctPatientIds)
            {
                ct.ThrowIfCancellationRequested();

                var patientContacts = (await _patientContactService.GetByPatientAsync(id))
                    .OrderByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.ContactType == ContactType.Self ? 0 : 1)
                    .ToList();

                var phone = patientContacts
                    .Select(x => x.PrimaryPhone)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (!string.IsNullOrWhiteSpace(phone))
                    contacts[id] = phone.Trim();
            }

            return contacts;
        }

        private static PatientReferralQueueItemDto ToQueueItem(
            PatientReferral entity,
            IReadOnlyDictionary<Guid, PatientDto> patients,
            IReadOnlyDictionary<Guid, string> contacts)
        {
            patients.TryGetValue(entity.PatientId, out var patient);
            contacts.TryGetValue(entity.PatientId, out var contact);

            var patientName = BuildPatientName(patient);
            var patientMrn = patient?.PatientId != Guid.Empty ? patient!.PatientId.ToString("D") : string.Empty;
            var status = NormalizeStatus(entity);
            var assignedToName = FirstNonEmpty(entity.AssignedToName, entity.SentTo);
            var referralSource = FirstNonEmpty(entity.ReferralSource, entity.SourceApp, entity.NoteType);
            var sourceApp = FirstNonEmpty(entity.SourceApp, entity.ReferralSource, "MedInsights");
            var caseTitle = FirstNonEmpty(entity.CaseTitle, entity.NoteTitle, "Patient referral");
            var caseSummary = FirstNonEmpty(entity.CaseSummary, entity.ReferralBody);
            var referralReason = FirstNonEmpty(entity.ReferralReason, entity.NoteTitle, entity.ReferralBody);
            var nextAction = FirstNonEmpty(entity.NextAction, status.Equals("New", StringComparison.OrdinalIgnoreCase) ? "Review referral" : "Continue referral");

            return new PatientReferralQueueItemDto
            {
                Id = entity.Id,
                PatientId = entity.PatientId,
                PatientName = patientName,
                PatientMrn = patientMrn,
                Status = status,
                Assignee = assignedToName,
                AssignedToName = assignedToName,
                OwnerRole = entity.OwnerRole,
                NextAction = nextAction,
                NextActionAt = entity.NextActionAt,
                DueAt = entity.DueAt ?? entity.NextActionAt,
                Priority = FirstNonEmpty(entity.Priority, "Routine"),
                ReferralSource = referralSource,
                SourceApp = sourceApp,
                ReferralChannel = entity.ReferralChannel,
                Diagnosis = entity.Diagnosis,
                CaseTitle = caseTitle,
                CaseSummary = caseSummary,
                ReferralReason = referralReason,
                Reason = referralReason,
                Contact = FirstNonEmpty(entity.Contact, contact ?? string.Empty),
                CreatedAt = EnsureUtc(entity.DateCreated),
                UpdatedAt = EnsureUtc(entity.DateUpdated)
            };
        }

        private static bool MatchesSearch(PatientReferralQueueItemDto item, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            var value = search.Trim();
            return Contains(item.PatientName, value)
                || Contains(item.PatientMrn, value)
                || Contains(item.Status, value)
                || Contains(item.AssignedToName, value)
                || Contains(item.CaseTitle, value)
                || Contains(item.CaseSummary, value)
                || Contains(item.ReferralReason, value)
                || Contains(item.ReferralSource, value)
                || Contains(item.Diagnosis, value);
        }

        private static bool Contains(string? source, string value)
            => !string.IsNullOrWhiteSpace(source)
                && source.Contains(value, StringComparison.OrdinalIgnoreCase);

        private static string NormalizeStatus(PatientReferral entity)
        {
            if (!string.IsNullOrWhiteSpace(entity.Status))
                return entity.Status.Trim();

            return entity.DateSent != default ? "Sent" : "New";
        }

        private static string BuildPatientName(PatientDto? patient)
        {
            if (patient is null)
                return string.Empty;

            return $"{patient.FirstName} {patient.LastName}".Trim();
        }

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

        private static DateTime EnsureUtc(DateTime value)
            => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private async Task<(Guid UserId, string FirstName, string LastName)> ResolveCreatorAsync()
        {
            var profile = await _userProfileService.GetAsync(_userContext.UserId);
            var firstName = FirstNonEmpty(_userContext.FirstName, profile?.FirstName ?? string.Empty);
            var lastName = FirstNonEmpty(_userContext.LastName, profile?.LastName ?? string.Empty);
            return (_userContext.UserId, firstName, lastName);
        }

        private static string BuildPersonName(string? firstName, string? lastName)
            => $"{firstName} {lastName}".Trim();

        private async Task AppendWorkflowActivitiesAsync(PatientReferral existing, PatientReferral updated)
        {
            if (!string.Equals(existing.Status, updated.Status, StringComparison.OrdinalIgnoreCase))
            {
                await _activityService.AppendAsync(new CreatePatientReferralActivityDto
                {
                    PatientReferralId = updated.Id,
                    PatientId = updated.PatientId,
                    ActivityType = PatientReferralActivityTypes.StatusChanged,
                    Title = "Referral status changed",
                    Body = $"{FirstNonEmpty(existing.Status, "New")} -> {FirstNonEmpty(updated.Status, "New")}",
                    Metadata = new Dictionary<string, string?>
                    {
                        ["previousStatus"] = existing.Status,
                        ["newStatus"] = updated.Status
                    }
                });
            }

            if (!string.Equals(existing.AssignedToName, updated.AssignedToName, StringComparison.OrdinalIgnoreCase))
            {
                await _activityService.AppendAsync(new CreatePatientReferralActivityDto
                {
                    PatientReferralId = updated.Id,
                    PatientId = updated.PatientId,
                    ActivityType = PatientReferralActivityTypes.OwnerChanged,
                    Title = "Referral owner changed",
                    Body = $"{FirstNonEmpty(existing.AssignedToName, "Unassigned")} -> {FirstNonEmpty(updated.AssignedToName, "Unassigned")}",
                    Metadata = new Dictionary<string, string?>
                    {
                        ["previousOwner"] = existing.AssignedToName,
                        ["newOwner"] = updated.AssignedToName
                    }
                });
            }

            if (existing.DateSent == default && updated.DateSent != default)
            {
                await _activityService.AppendAsync(new CreatePatientReferralActivityDto
                {
                    PatientReferralId = updated.Id,
                    PatientId = updated.PatientId,
                    ActivityType = PatientReferralActivityTypes.ReferralSent,
                    Title = "Referral sent",
                    Body = FirstNonEmpty(updated.SentTo, updated.ReferralReason, updated.CaseSummary, updated.ReferralBody),
                    CreatedAtUtc = updated.DateSent
                });
            }

            if (WorkflowChanged(existing, updated))
            {
                await _activityService.AppendAsync(new CreatePatientReferralActivityDto
                {
                    PatientReferralId = updated.Id,
                    PatientId = updated.PatientId,
                    ActivityType = PatientReferralActivityTypes.WorkflowUpdated,
                    Title = "Referral workflow updated",
                    Body = FirstNonEmpty(updated.NextAction, updated.CaseSummary, updated.ReferralReason, updated.ReferralBody)
                });
            }
        }

        private static bool WorkflowChanged(PatientReferral existing, PatientReferral updated)
        {
            return !string.Equals(existing.Status, updated.Status, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.AssignedToName, updated.AssignedToName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.OwnerRole, updated.OwnerRole, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.NextAction, updated.NextAction, StringComparison.Ordinal)
                || existing.NextActionAt != updated.NextActionAt
                || existing.DueAt != updated.DueAt
                || !string.Equals(existing.Priority, updated.Priority, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.ReferralChannel, updated.ReferralChannel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
