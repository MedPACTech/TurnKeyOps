using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class ReferralWorkItemService : IReferralWorkItemService
    {
        private readonly IReferralWorkItemRepository _repository;
        private readonly IUserContext _userContext;
        private readonly IPatientClinicalSummaryService _patientClinicalSummaryService;

        public ReferralWorkItemService(
            IReferralWorkItemRepository repository,
            IUserContext userContext,
            IPatientClinicalSummaryService patientClinicalSummaryService)
        {
            _repository = repository;
            _userContext = userContext;
            _patientClinicalSummaryService = patientClinicalSummaryService;
        }

        public async Task<IReadOnlyList<ReferralWorkItemDto>> GetAllAsync(
            Guid? patientId = null,
            Guid? encounterId = null,
            string? status = null,
            string? search = null,
            CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var items = await _repository.GetByTenantAsync(_userContext.TenantId, patientId, encounterId, status, search, ct);
            return items.Select(ReferralWorkItemMapper.ToDto).ToList();
        }

        public async Task<ReferralWorkItemDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = await _repository.GetAsync(_userContext.TenantId, id, ct);
            return entity is null ? null : ReferralWorkItemMapper.ToDto(entity);
        }

        public async Task<ReferralWorkItemDto> CreateAsync(CreateReferralWorkItemDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = ReferralWorkItemMapper.ToEntity(dto);
            entity.Id = Guid.NewGuid();
            entity.TenantId = _userContext.TenantId;
            entity.PartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);

            var now = DateTime.UtcNow;
            entity.DateCreated = now;
            entity.DateUpdated = now;
            entity.LastUpdate = string.IsNullOrWhiteSpace(entity.LastUpdate) ? ToDisplayTime(now) : entity.LastUpdate;
            entity.LastUpdateNote = string.IsNullOrWhiteSpace(entity.LastUpdateNote) ? "Referral created." : entity.LastUpdateNote;
            entity.LatestNoteAuthor = string.IsNullOrWhiteSpace(entity.LatestNoteAuthor) ? CurrentActor() : entity.LatestNoteAuthor;

            var saved = await _repository.SaveAsync(entity, ct);
            return ReferralWorkItemMapper.ToDto(saved);
        }

        public async Task<ReferralWorkItemDto> UpdateAsync(Guid id, UpdateReferralWorkItemDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            if (id != dto.Id)
                throw new ArgumentException("ID mismatch.", nameof(dto));

            var existing = await GetRequiredAsync(id, ct);
            var updated = ReferralWorkItemMapper.ToEntity(dto);

            CopyMutableFields(existing, updated);
            existing.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(existing, ct);
            return ReferralWorkItemMapper.ToDto(saved);
        }

        public async Task<ReferralWorkItemDto> UpdateWorkflowAsync(Guid id, UpdateReferralWorkflowDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = await GetRequiredAsync(id, ct);
            entity.Status = NormalizeRequired(dto.Status, nameof(dto.Status));
            entity.Assignee = NormalizeRequired(dto.Assignee, nameof(dto.Assignee));
            if (!string.IsNullOrWhiteSpace(dto.OwnerRole))
                entity.OwnerRole = dto.OwnerRole.Trim();
            entity.NextAction = NormalizeRequired(dto.NextAction, nameof(dto.NextAction));
            entity.NextActionAt = NormalizeRequired(dto.NextActionAt, nameof(dto.NextActionAt));
            entity.LastUpdate = ToDisplayTime(DateTime.UtcNow);
            entity.LastUpdateNote = $"Workflow updated for {entity.Status}.";
            entity.LatestNoteAuthor = CurrentActor();
            entity.DateUpdated = DateTime.UtcNow;

            AppendTimeline(entity, new ReferralTimelineItemDto
            {
                Label = "Workflow updated",
                At = entity.LastUpdate,
                Note = $"{entity.Status} assigned to {entity.Assignee}."
            });

            var saved = await _repository.SaveAsync(entity, ct);
            return ReferralWorkItemMapper.ToDto(saved);
        }

        public async Task<ReferralWorkItemDto> AddActionAsync(Guid id, ReferralWorkItemActionDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = await GetRequiredAsync(id, ct);
            var action = NormalizeRequired(dto.Action, nameof(dto.Action)).ToLowerInvariant();
            var now = DateTime.UtcNow;
            var actor = string.IsNullOrWhiteSpace(dto.PerformedBy) ? CurrentActor() : dto.PerformedBy.Trim();
            var note = string.IsNullOrWhiteSpace(dto.Note) ? BuildDefaultActionNote(action, entity) : dto.Note.Trim();
            var label = action switch
            {
                "note" => "Note added",
                "nudge" => "Owner nudged",
                _ => throw new ArgumentException("Unsupported referral action.", nameof(dto))
            };

            entity.LastUpdate = ToDisplayTime(now);
            entity.LastUpdateNote = note;
            entity.LatestNoteAuthor = actor;
            entity.DateUpdated = now;

            AppendTimeline(entity, new ReferralTimelineItemDto
            {
                Label = label,
                At = entity.LastUpdate,
                Note = note
            });

            var saved = await _repository.SaveAsync(entity, ct);
            return ReferralWorkItemMapper.ToDto(saved);
        }

        public async Task<ReferralWorkItemDto> RefreshCaseSummaryAsync(Guid id, bool forceRefresh = false, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = await GetRequiredAsync(id, ct);
            if (!entity.PatientId.HasValue || entity.PatientId.Value == Guid.Empty)
                throw new InvalidOperationException("Referral must have a patientId before refreshing case summary.");

            var summary = await _patientClinicalSummaryService.GenerateAsync(entity.PatientId.Value, forceRefresh, ct);
            var refreshedSummary = string.IsNullOrWhiteSpace(summary.ReferralCaseSummary)
                ? summary.Narrative
                : summary.ReferralCaseSummary;

            var now = DateTime.UtcNow;
            entity.CaseSummary = refreshedSummary;
            entity.LastUpdate = ToDisplayTime(now);
            entity.LastUpdateNote = "Case summary refreshed from patient chart context.";
            entity.LatestNoteAuthor = CurrentActor();
            entity.DateUpdated = now;

            AppendTimeline(entity, new ReferralTimelineItemDto
            {
                Label = "Case summary refreshed",
                At = entity.LastUpdate,
                Note = Clip(refreshedSummary, 220)
            });

            var saved = await _repository.SaveAsync(entity, ct);
            return ReferralWorkItemMapper.ToDto(saved);
        }

        private async Task<ReferralWorkItem> GetRequiredAsync(Guid id, CancellationToken ct)
            => await _repository.GetAsync(_userContext.TenantId, id, ct)
                ?? throw new KeyNotFoundException("Referral not found.");

        private static void CopyMutableFields(ReferralWorkItem target, ReferralWorkItem source)
        {
            target.PatientId = source.PatientId;
            target.EncounterId = source.EncounterId;
            target.PatientName = source.PatientName;
            target.Mrn = source.Mrn;
            target.ReferralSource = source.ReferralSource;
            target.ReferralChannel = source.ReferralChannel;
            target.SourceReceivedAt = source.SourceReceivedAt;
            target.CaseTitle = source.CaseTitle;
            target.CaseSummary = source.CaseSummary;
            target.Diagnosis = source.Diagnosis;
            target.Priority = source.Priority;
            target.Status = source.Status;
            target.Assignee = source.Assignee;
            target.OwnerRole = source.OwnerRole;
            target.NextAction = source.NextAction;
            target.NextActionAt = source.NextActionAt;
            target.LastUpdate = source.LastUpdate;
            target.LastUpdateNote = source.LastUpdateNote;
            target.Signal = source.Signal;
            target.ReasonInQueue = source.ReasonInQueue;
            target.QueueLane = source.QueueLane;
            target.BlockerLabel = source.BlockerLabel;
            target.DueLabel = source.DueLabel;
            target.DueClock = source.DueClock;
            target.Contact = source.Contact;
            target.PatientDetailsJson = source.PatientDetailsJson;
            target.LatestNoteAuthor = source.LatestNoteAuthor;
            target.TimelineJson = source.TimelineJson;
        }

        private static void AppendTimeline(ReferralWorkItem entity, ReferralTimelineItemDto item)
        {
            var timeline = ReferralWorkItemMapper.ToDto(entity).Timeline;
            timeline.Add(item);
            entity.TimelineJson = JsonSerializer.Serialize(timeline);
        }

        private static string NormalizeRequired(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{fieldName} is required.", fieldName);

            return value.Trim();
        }

        private static string BuildDefaultActionNote(string action, ReferralWorkItem entity)
            => action switch
            {
                "note" => $"Referral note added for {entity.PatientName}.",
                "nudge" => $"Nudged {entity.Assignee} about '{entity.NextAction}'.",
                _ => "Referral updated."
            };

        private static string Clip(string? value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            if (trimmed.Length <= maxChars)
                return trimmed;

            return trimmed[..maxChars].TrimEnd() + "...";
        }

        private string CurrentActor()
        {
            var fullName = $"{_userContext.FirstName} {_userContext.LastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? _userContext.UserId.ToString() : fullName;
        }

        private static string ToDisplayTime(DateTime utc)
            => utc.ToString("MMM d, h:mm tt 'UTC'");

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }
    }
}
