using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using Microsoft.AspNetCore.Routing;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace MedInsights.Services
{
    public class CaptureDraftNoteService : ICaptureDraftNoteService
    {
        private const string CaptureStatusDraft = "Draft";
        private const string CaptureStatusInProgress = "InProgress";
        private const string CaptureStatusCompleted = "Completed";

        private readonly ICaptureDraftNoteRepository _repo;
        private readonly IPatientEncounterRepository _encounterRepository;
        private readonly IPatientBillingNoteRepository _patientBillingNoteRepository;
        private readonly IPatientReferralRepository _patientReferralRepository;
        private readonly IPatientClinicalSummaryService _patientClinicalSummaryService;
        private readonly INoteTypePromptBuilderService _noteTypePromptBuilderService;
        private readonly IUserProfileService _userProfileService;
        private readonly IUserContext _userContext;
        private readonly IAIService<OpenAIChatMessage> _ai;

        private readonly IActivityLogService _activityLogService;

        public CaptureDraftNoteService(
            ICaptureDraftNoteRepository repo,
            IPatientEncounterRepository patientEncounterRepository,
            IPatientBillingNoteRepository patientBillingNoteRepository,
            IPatientReferralRepository patientReferralRepository,
            IPatientClinicalSummaryService patientClinicalSummaryService,
            INoteTypePromptBuilderService noteTypePromptBuilderService,
            IUserProfileService userProfileService,
            IUserContext userContext,
            IActivityLogService activityLogService,
            IAIService<OpenAIChatMessage> ai)
        {
            _repo = repo;
            _encounterRepository = patientEncounterRepository;
            _patientBillingNoteRepository = patientBillingNoteRepository;
            _patientReferralRepository = patientReferralRepository;
            _patientClinicalSummaryService = patientClinicalSummaryService;
            _noteTypePromptBuilderService = noteTypePromptBuilderService;
            _userProfileService = userProfileService;
            _userContext = userContext;
            _activityLogService = activityLogService;
            _ai = ai;
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private string PartitionKeyForCurrent()
            => EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);

        private static string RowKey(Guid id)
            => EntityKeyPolicy.Row(id);

        private static string GuidOrEmpty(Guid? id)
            => id.HasValue ? EntityKeyPolicy.Row(id.Value) : string.Empty;

        private static string GuidOrNull(Guid? id)
            => id.HasValue ? EntityKeyPolicy.Row(id.Value) : null;

        private static string NormalizeCaptureStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim();
            if (normalized.Equals("Draft", StringComparison.OrdinalIgnoreCase))
                return CaptureStatusDraft;
            if (normalized.Equals("Generated", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("InProgress", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("In Progress", StringComparison.OrdinalIgnoreCase))
                return CaptureStatusInProgress;
            if (normalized.Equals("Signed", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Completed", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Complete", StringComparison.OrdinalIgnoreCase))
                return CaptureStatusCompleted;

            return CaptureStatusDraft;
        }

        private static CaptureDraftNoteDto ToDto(CaptureDraftNote entity)
        {
            var dto = CaptureDraftNoteMapper.ToDto(entity);
            dto.CaptureStatus = NormalizeCaptureStatus(entity.CaptureStatus);
            return dto;
        }

        /// <summary>
        /// Create a brand new draft note.
        /// NOTE: ProviderId is always set from user context.
        /// PatientId is optional.
        /// </summary>
        public async Task<CaptureDraftNoteDto> AddAsync(CaptureDraftNoteDto dto)
        {
            EnsureAuthenticated();

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.NoteType))
                throw new InvalidOperationException("NoteType is required.");

            // ProviderId ALWAYS from context (DTO may be empty/optional)
            dto.ProviderId = _userContext.UserId;

            if (dto.Id == Guid.Empty)
                dto.Id = Guid.NewGuid();

            if (dto.CreatedBy == Guid.Empty)
                dto.CreatedBy = _userContext.UserId;

            if (dto.DateCreated == default)
                dto.DateCreated = DateTime.UtcNow;

            dto.CaptureStatus = CaptureStatusDraft;

            var entity = CaptureDraftNoteMapper.ToEntity(dto, PartitionKeyForCurrent());
            var saved = await _repo.SaveAsync(entity);

            return ToDto(saved);
        }

        public async Task<CaptureDraftNoteDto?> GetAsync(Guid id)
        {
            EnsureAuthenticated();

            var partitionKey = PartitionKeyForCurrent();
            var entity = await _repo.GetAsync(partitionKey, RowKey(id));
            return entity == null ? null : ToDto(entity);
        }

        public async Task<List<CaptureDraftNoteDto>> GetMineAsync()
        {
            EnsureAuthenticated();

            var partitionKey = PartitionKeyForCurrent();
            var entities = await _repo.GetByPartitionAsync(partitionKey);
            return entities.Select(ToDto).ToList();
        }

        public async Task<List<CaptureDraftNoteDto>> GetRecentAsync(int take = 10, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var normalizedTake = take <= 0 ? 10 : Math.Min(take, 50);
            var partitionKey = PartitionKeyForCurrent();
            var entities = await _repo.GetByPartitionAsync(partitionKey, ct);

            return entities
                .OrderByDescending(x => x.DateUpdated == default ? x.DateCreated : x.DateUpdated)
                .ThenByDescending(x => x.DateCreated)
                .Take(normalizedTake)
                .Select(ToDto)
                .ToList();
        }

        public async Task<List<CaptureDraftNoteDto>> GetMineByPatientAsync(Guid patientId)
        {
            EnsureAuthenticated();

            var partitionKey = PartitionKeyForCurrent();
            var patientKey = EntityKeyPolicy.Row(patientId);
            var entities = await _repo.GetByPartitionAndPatientAsync(partitionKey, patientKey);
            return entities.Select(ToDto).ToList();
        }

        /// <summary>
        /// "Post changes" style save.
        /// If dto.Id is empty => create.
        /// If dto.Id exists => fetch and update in place, then UpdateAsync.
        /// </summary>
        public async Task<CaptureDraftNoteDto> UpdateAsync(CaptureDraftNoteDto dto)
        {
            EnsureAuthenticated();

            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            // Treat UpdateAsync as Save/Upsert:
            if (dto.Id == Guid.Empty)
                return await AddAsync(dto);

            // ProviderId ALWAYS from context (DTO may be empty/optional)
            dto.ProviderId = _userContext.UserId;

            if (string.IsNullOrWhiteSpace(dto.NoteType))
                throw new InvalidOperationException("NoteType is required.");

            var partitionKey = PartitionKeyForCurrent();
            var rowKey = RowKey(dto.Id);

            var existing = await _repo.GetAsync(partitionKey, rowKey);

            if (existing == null)
                throw new KeyNotFoundException("Draft note not found.");

            var status = NormalizeCaptureStatus(existing.CaptureStatus);
            existing.CaptureStatus = status;
            if (status.Equals(CaptureStatusCompleted, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Completed notes cannot be edited.");
            }

            // Update in-place (preserve created fields)
            existing.PatientId = GuidOrEmpty(dto.PatientId);
            existing.ProviderId = EntityKeyPolicy.Row(dto.ProviderId.Value);

            existing.CaptureSourceType = dto.CaptureSourceType ?? string.Empty;
            existing.CaptureSourceId = GuidOrNull(dto.CaptureSourceId);
            existing.CaptureSourceText = dto.CaptureSourceText ?? string.Empty;
            existing.CaptureSourceAddendum = dto.CaptureSourceAddendum ?? string.Empty;
            // Status is workflow-managed: Draft -> InProgress -> Completed.
            existing.CaptureStatus = status;

            existing.NoteType = dto.NoteType ?? string.Empty;
            existing.NoteTitle = dto.NoteTitle ?? string.Empty;

            // When generated content exists, avoid wiping it with empty payload fields from autosave/update calls.
            var hasGeneratedContent = status.Equals(CaptureStatusInProgress, StringComparison.Ordinal);
            if (!hasGeneratedContent || !string.IsNullOrWhiteSpace(dto.NoteBody))
                existing.NoteBody = dto.NoteBody ?? string.Empty;

            if (!hasGeneratedContent || !string.IsNullOrWhiteSpace(dto.BillingBody))
                existing.BillingBody = dto.BillingBody ?? string.Empty;

            if (!hasGeneratedContent || !string.IsNullOrWhiteSpace(dto.CommunicationBody))
                existing.CommunicationBody = dto.CommunicationBody ?? string.Empty;

            existing.Tags = dto.Tags ?? string.Empty;
            existing.DateUpdated = DateTime.UtcNow;
            existing.PartitionKey = partitionKey;
            existing.RowKey = rowKey;

            var saved = await _repo.SaveAsync(existing);

            return ToDto(saved);
        }

        public async Task<CaptureDraftNoteDto> GenerateNoteAsync(CaptureDraftNoteDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            if (dto.Id == Guid.Empty)
                throw new ArgumentException("Draft note id is required.", nameof(dto.Id));

            var template = (dto.NoteType ?? "").Trim();
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Template is required.", nameof(template));

            var promptProfile = await _noteTypePromptBuilderService.ResolveAsync(template, ct);

            var partitionKey = PartitionKeyForCurrent();
            var rowKey = RowKey(dto.Id);

            var existing = await _repo.GetAsync(partitionKey, rowKey);
            if (existing == null)
                throw new KeyNotFoundException("Draft note not found.");

            // Patient must be selected before generating.
            // Prefer persisted value, but allow request value when UI calls generate immediately after edits.
            var patientId = (existing.PatientId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(patientId) || patientId == "null" || patientId == "undefined")
                patientId = (dto.PatientId?.ToString() ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(patientId) || patientId == "null" || patientId == "undefined")
                throw new InvalidOperationException("Patient is not selected for this draft note.");

            // Transcript / narrative must exist.
            // Prefer persisted text, but fall back to request payload when draft update and generate happen back-to-back.
            var sourceText = (existing.CaptureSourceText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(sourceText))
                sourceText = (dto.CaptureSourceText ?? "").Trim();

            var addendum = (existing.CaptureSourceAddendum ?? "").Trim();
            if (string.IsNullOrWhiteSpace(addendum))
                addendum = (dto.CaptureSourceAddendum ?? "").Trim();

            var transcript = sourceText;
            if (!string.IsNullOrWhiteSpace(addendum))
                transcript = transcript + "\n\nAddendum:\n" + addendum;

            if (string.IsNullOrWhiteSpace(transcript))
                throw new InvalidOperationException("Transcript is not available yet.");

            var systemPrompt = _noteTypePromptBuilderService.BuildSystemPrompt(promptProfile);

            var userMessages = new[]
            {
                $"Template: {promptProfile.DisplayName}",
                $"PatientId: {patientId}",
                "Transcript:\n" + transcript
            };

            const int maxTokens = 1400;
            const double temperature = 0.1;

            var noteText = await _ai.GetChatCompletionAsync(
                systemPrompt: systemPrompt,
                userMessages: userMessages,
                maxOutputTokens: maxTokens,
                temperature: temperature,
                ct: ct
            );

            var cleaned = (noteText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
                throw new InvalidOperationException("AI returned an empty note.");

            // ✅ Split into the 3 bodies based on required headers
            var parts = _noteTypePromptBuilderService.SplitOutput(cleaned, promptProfile);

            existing.NoteType = promptProfile.DisplayName;
            existing.NoteTitle = $"{promptProfile.DisplayName} - {DateTime.UtcNow:yyyy-MM-dd}";
            existing.PatientId = patientId;
            existing.CaptureSourceText = sourceText;
            existing.CaptureSourceAddendum = addendum;

            existing.NoteBody = parts.ClinicalNote;
            existing.BillingBody = parts.BillingRecommendations;
            existing.CommunicationBody = parts.ExternalCommunication;

            existing.CaptureStatus = CaptureStatusInProgress;
            existing.DateUpdated = DateTime.UtcNow;
            existing.PartitionKey = partitionKey;
            existing.RowKey = rowKey;

            var saved = await _repo.SaveAsync(existing, ct);

            return ToDto(saved);
        }

        public async Task<PatientReferralDto> CreateReferralAsync(Guid captureDraftNoteId, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            if (captureDraftNoteId == Guid.Empty)
                throw new ArgumentException("captureDraftNoteId is required.", nameof(captureDraftNoteId));

            var draft = await _repo.GetAsync(PartitionKeyForCurrent(), RowKey(captureDraftNoteId), ct)
                ?? throw new KeyNotFoundException("Capture draft note not found.");

            if (draft.IsDeleted)
                throw new InvalidOperationException("Capture draft note is deleted.");

            var status = NormalizeCaptureStatus(draft.CaptureStatus);
            var hasGeneratedBodies =
                !string.IsNullOrWhiteSpace(draft.NoteBody) ||
                !string.IsNullOrWhiteSpace(draft.BillingBody);

            var isEligibleStatus =
                status.Equals(CaptureStatusInProgress, StringComparison.Ordinal) ||
                status.Equals(CaptureStatusCompleted, StringComparison.Ordinal);

            if (!isEligibleStatus && !hasGeneratedBodies)
                throw new InvalidOperationException("Referral can only be created after the encounter note has been generated.");

            if (string.IsNullOrWhiteSpace(draft.PatientId) || !Guid.TryParse(draft.PatientId.Trim(), out var patientId))
                throw new InvalidOperationException("Patient is required before creating a referral.");

            if (string.IsNullOrWhiteSpace(draft.ProviderId) || !Guid.TryParse(draft.ProviderId.Trim(), out var providerId))
                throw new InvalidOperationException("Provider is required before creating a referral.");

            var summary = await TryGenerateClinicalSummaryAsync(patientId, ct);

            var existingReferral = await _patientReferralRepository.GetByCaptureDraftNoteIdAsync(_userContext.TenantId, draft.Id, ct);
            if (existingReferral != null)
            {
                existingReferral.PartitionKey = EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);
                existingReferral.PatientId = patientId;
                existingReferral.ProviderId = providerId;
                existingReferral.NoteType = draft.NoteType ?? string.Empty;
                existingReferral.NoteTitle = draft.NoteTitle ?? string.Empty;
                existingReferral.ReferralBody = draft.CommunicationBody ?? string.Empty;
                existingReferral.CaseSummary = ResolveCaseSummary(existingReferral.CaseSummary, draft, summary);
                existingReferral.ReferralReason = ResolveReferralReason(existingReferral.ReferralReason, draft, summary);
                existingReferral.Status = string.IsNullOrWhiteSpace(existingReferral.Status) ? "Pending" : existingReferral.Status;
                existingReferral.DateUpdated = DateTime.UtcNow;

                var savedExisting = await _patientReferralRepository.SaveAsync(existingReferral, ct);
                return PatientReferralMapper.ToDto(savedExisting);
            }

            var creator = await ResolveCreatorAsync();
            var referral = new PatientReferral
            {
                Id = Guid.NewGuid(),
                PartitionKey = EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId),
                RowKey = string.Empty,
                CaptureDraftNoteId = draft.Id,
                PatientId = patientId,
                ProviderId = providerId,
                NoteType = draft.NoteType ?? string.Empty,
                NoteTitle = draft.NoteTitle ?? string.Empty,
                ReferralBody = draft.CommunicationBody ?? string.Empty,
                CaseSummary = ResolveCaseSummary(string.Empty, draft, summary),
                ReferralReason = ResolveReferralReason(string.Empty, draft, summary),
                Status = "Pending",
                CreatedByUserId = creator.UserId,
                CreatedByFirstName = creator.FirstName,
                CreatedByLastName = creator.LastName,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                IsDeleted = false
            };

            referral.RowKey = EntityKeyPolicy.Row(referral.Id);
            var savedReferral = await _patientReferralRepository.SaveAsync(referral, ct);
            return PatientReferralMapper.ToDto(savedReferral);
        }

        // Sign the draft and persist billing/referral records.
        public async Task<CaptureDraftNoteDto> SignAsync(Guid captureDraftNoteId, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            if (captureDraftNoteId == Guid.Empty)
                throw new ArgumentException("captureDraftNoteId is required.", nameof(captureDraftNoteId));

            var pk = PartitionKeyForCurrent();
            var rk = RowKey(captureDraftNoteId);

            var draft = await _repo.GetAsync(pk, rk);
            if (draft == null)
                throw new KeyNotFoundException("Capture draft note not found.");

            if (draft.IsDeleted)
                throw new InvalidOperationException("Capture draft note is deleted.");

            var status = NormalizeCaptureStatus(draft.CaptureStatus);
            draft.CaptureStatus = status;

            if (status.Equals(CaptureStatusCompleted, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("This draft note is already completed.");
            }

            var hasGeneratedBodies =
                !string.IsNullOrWhiteSpace(draft.NoteBody) ||
                !string.IsNullOrWhiteSpace(draft.BillingBody) ||
                !string.IsNullOrWhiteSpace(draft.CommunicationBody);

            if (!status.Equals(CaptureStatusInProgress, StringComparison.Ordinal) && !hasGeneratedBodies)
            {
                var parsedDraftPatientId = Guid.Empty;
                var hasDraftPatientId = !string.IsNullOrWhiteSpace(draft.PatientId) &&
                                        Guid.TryParse(draft.PatientId.Trim(), out parsedDraftPatientId);

                // Resilience for client-side autosave races that may leave status at Draft.
                var canAttemptAutoGenerate =
                    !string.IsNullOrWhiteSpace(draft.NoteType) &&
                    !string.IsNullOrWhiteSpace(draft.CaptureSourceText) &&
                    hasDraftPatientId;

                if (canAttemptAutoGenerate)
                {
                    await GenerateNoteAsync(new CaptureDraftNoteDto
                    {
                        Id = draft.Id,
                        NoteType = draft.NoteType,
                        PatientId = parsedDraftPatientId,
                        CaptureSourceText = draft.CaptureSourceText,
                        CaptureSourceAddendum = draft.CaptureSourceAddendum
                    }, ct);

                    draft = await _repo.GetAsync(pk, rk)
                        ?? throw new KeyNotFoundException("Capture draft note not found after note generation.");

                    status = NormalizeCaptureStatus(draft.CaptureStatus);
                    draft.CaptureStatus = status;
                    hasGeneratedBodies =
                        !string.IsNullOrWhiteSpace(draft.NoteBody) ||
                        !string.IsNullOrWhiteSpace(draft.BillingBody) ||
                        !string.IsNullOrWhiteSpace(draft.CommunicationBody);
                }
            }

            if (!status.Equals(CaptureStatusInProgress, StringComparison.Ordinal) && !hasGeneratedBodies)
                throw new InvalidOperationException($"Only in-progress notes can be completed. Current status: '{status}'.");

            if (string.IsNullOrWhiteSpace(draft.PatientId))
                throw new InvalidOperationException("Patient is required before signing.");

            if (!Guid.TryParse(draft.PatientId.Trim(), out var patientId))
                throw new InvalidOperationException("Draft note PatientId is invalid.");

            if (string.IsNullOrWhiteSpace(draft.ProviderId))
                throw new InvalidOperationException("ProviderId is required.");

            if (!Guid.TryParse(draft.ProviderId.Trim(), out var providerId))
                throw new InvalidOperationException("Draft note ProviderId is invalid.");

            // Prevent signing if clinical body is empty
            if (string.IsNullOrWhiteSpace(draft.NoteBody))
                throw new InvalidOperationException("Cannot sign an empty clinical note.");

            var tenantPatientPartition = EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);
            var signedAt = DateTime.UtcNow;

            var patientEncounter = new PatientEncounter
            {
                Id = Guid.NewGuid(),
                PartitionKey = tenantPatientPartition,
                RowKey = string.Empty,
                PatientId = EntityKeyPolicy.Row(patientId),
                EncounterBody = draft.NoteBody ?? string.Empty,
                CaptureDraftNoteId = draft.Id,
                ProviderId = providerId,
                NoteType = draft.NoteType ?? string.Empty,
                NoteTitle = draft.NoteTitle ?? string.Empty,
                Data = "{}",
                Status = "Signed",
                DateCreated = draft.DateCreated == default ? signedAt : draft.DateCreated,
                DateUpdated = signedAt,
                IsDeleted = false
            };
            patientEncounter.RowKey = EntityKeyPolicy.Row(patientEncounter.Id);
            var savedPatientEncounter = await _encounterRepository.SaveAsync(patientEncounter, ct);

            var patientBillingNote = new PatientBillingNote
            {
                Id = Guid.NewGuid(),
                PartitionKey = tenantPatientPartition,
                RowKey = string.Empty,
                EncounterId = savedPatientEncounter.Id,
                CaptureDraftNoteId = draft.Id,
                PatientId = patientId,
                ProviderId = providerId,
                NoteType = draft.NoteType ?? string.Empty,
                NoteTitle = draft.NoteTitle ?? string.Empty,
                BillingBody = draft.BillingBody ?? string.Empty,
                DateSigned = signedAt,
                SignedBy = _userContext.UserId,
                DateCreated = draft.DateCreated == default ? signedAt : draft.DateCreated,
                DateUpdated = signedAt,
                IsDeleted = false
            };
            patientBillingNote.RowKey = EntityKeyPolicy.Row(patientBillingNote.Id);
            await _patientBillingNoteRepository.SaveAsync(patientBillingNote, ct);

            var promptProfile = await _noteTypePromptBuilderService.ResolveAsync(draft.NoteType ?? string.Empty, ct);
            await UpsertReferralOnSignAsync(
                draft,
                savedPatientEncounter.Id,
                patientId,
                providerId,
                signedAt,
                promptProfile.AlwaysCreateReferral,
                ct);

            // Mark draft as completed after signed encounter creation.
            draft.CaptureStatus = CaptureStatusCompleted;
            draft.DateUpdated = DateTime.UtcNow;
            draft.PartitionKey = pk;
            draft.RowKey = rk;

            await _repo.SaveAsync(draft, ct);

            // ---------------------------------------------------------
            // Activity tracking: Signed Note
            // ---------------------------------------------------------
            await _activityLogService.UpsertAsync(new ActivityLogUpsertDto
            {
                EntryDate = DateTime.UtcNow.Date,
                FacilityId = null,

                TenantId = _userContext.TenantId,
                UserId = _userContext.UserId,
                EnteredBy = _userContext.UserId,

                Items = new List<ActivityLogItemDto>
                {
                    new ActivityLogItemDto
                    {
                        Key = draft.NoteType,
                        Type = draft.CaptureSourceType,
                        Value = 1,
                        Unit = "count",

                        UserFirstName = _userContext.FirstName,
                        UserLastName  = _userContext.LastName
                    }
                }
            }, ct);

            return ToDto(draft);
        }

        private async Task PromoteReferralForReviewAsync(
            PatientReferral referral,
            CaptureDraftNote draft,
            Guid encounterId,
            Guid patientId,
            Guid providerId,
            DateTime completedAtUtc,
            CancellationToken ct)
        {
            referral.EncounterId = encounterId;
            referral.PartitionKey = EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);
            referral.PatientId = patientId;
            referral.ProviderId = providerId;
            referral.NoteType = draft.NoteType ?? string.Empty;
            referral.NoteTitle = draft.NoteTitle ?? string.Empty;
            referral.ReferralBody = draft.CommunicationBody ?? string.Empty;
            var summary = await TryGenerateClinicalSummaryAsync(patientId, ct);
            referral.CaseSummary = ResolveCaseSummary(referral.CaseSummary, draft, summary);
            referral.ReferralReason = ResolveReferralReason(referral.ReferralReason, draft, summary);
            referral.Status = "Ready for Review";
            referral.DateUpdated = completedAtUtc;

            await _patientReferralRepository.SaveAsync(referral, ct);
        }

        private async Task UpsertReferralOnSignAsync(
            CaptureDraftNote draft,
            Guid encounterId,
            Guid patientId,
            Guid providerId,
            DateTime signedAtUtc,
            bool alwaysCreateReferral,
            CancellationToken ct)
        {
            var existingReferral = await _patientReferralRepository.GetByCaptureDraftNoteIdAsync(_userContext.TenantId, draft.Id, ct);
            if (existingReferral is not null)
            {
                await PromoteReferralForReviewAsync(existingReferral, draft, encounterId, patientId, providerId, signedAtUtc, ct);
                return;
            }

            if (!alwaysCreateReferral)
            {
                return;
            }

            var creator = await ResolveCreatorAsync();
            var summary = await TryGenerateClinicalSummaryAsync(patientId, ct);
            var referral = new PatientReferral
            {
                Id = Guid.NewGuid(),
                PartitionKey = EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId),
                RowKey = string.Empty,
                EncounterId = encounterId,
                CaptureDraftNoteId = draft.Id,
                PatientId = patientId,
                ProviderId = providerId,
                NoteType = draft.NoteType ?? string.Empty,
                NoteTitle = draft.NoteTitle ?? string.Empty,
                ReferralBody = draft.CommunicationBody ?? string.Empty,
                CaseSummary = ResolveCaseSummary(string.Empty, draft, summary),
                ReferralReason = ResolveReferralReason(string.Empty, draft, summary),
                Status = "Ready for Review",
                CreatedByUserId = creator.UserId,
                CreatedByFirstName = creator.FirstName,
                CreatedByLastName = creator.LastName,
                DateCreated = signedAtUtc,
                DateUpdated = signedAtUtc,
                IsDeleted = false
            };

            referral.RowKey = EntityKeyPolicy.Row(referral.Id);
            await _patientReferralRepository.SaveAsync(referral, ct);
        }

        private async Task<(Guid UserId, string FirstName, string LastName)> ResolveCreatorAsync()
        {
            var profile = await _userProfileService.GetAsync(_userContext.UserId);
            var firstName = FirstNonEmpty(_userContext.FirstName, profile?.FirstName ?? string.Empty);
            var lastName = FirstNonEmpty(_userContext.LastName, profile?.LastName ?? string.Empty);
            return (_userContext.UserId, firstName, lastName);
        }

        private async Task<PatientClinicalSummaryDto?> TryGenerateClinicalSummaryAsync(Guid patientId, CancellationToken ct)
        {
            try
            {
                return await _patientClinicalSummaryService.GenerateAsync(patientId, false, ct);
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveCaseSummary(string existingValue, CaptureDraftNote draft, PatientClinicalSummaryDto? summary)
            => FirstNonEmpty(
                existingValue,
                summary?.ReferralCaseSummary ?? string.Empty,
                draft.CommunicationBody ?? string.Empty,
                draft.NoteBody ?? string.Empty);

        private static string ResolveReferralReason(string existingValue, CaptureDraftNote draft, PatientClinicalSummaryDto? summary)
            => FirstNonEmpty(
                existingValue,
                summary?.ReferralReason ?? string.Empty,
                draft.NoteTitle ?? string.Empty,
                draft.CommunicationBody ?? string.Empty,
                draft.NoteBody ?? string.Empty);

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;



        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var existing = await _repo.GetAsync(PartitionKeyForCurrent(), RowKey(id), ct)
                ?? throw new KeyNotFoundException("Draft note not found.");

            await RemovePendingReferralAsync(existing, ct);
            existing.IsDeleted = true;
            existing.DateUpdated = DateTime.UtcNow;
            await _repo.SaveAsync(existing, ct);
            return true;
        }

        private async Task RemovePendingReferralAsync(CaptureDraftNote draft, CancellationToken ct)
        {
            var existingReferral = await _patientReferralRepository.GetByCaptureDraftNoteIdAsync(_userContext.TenantId, draft.Id, ct);
            if (existingReferral == null || !string.Equals(existingReferral.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                return;

            existingReferral.IsDeleted = true;
            existingReferral.DateUpdated = DateTime.UtcNow;
            await _patientReferralRepository.SaveAsync(existingReferral, ct);
        }
    }
}

