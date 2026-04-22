using System.Text.Json;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class ReferralWorkItemMapper
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static ReferralWorkItemDto ToDto(ReferralWorkItem entity)
        {
            return new ReferralWorkItemDto
            {
                Id = entity.Id,
                PatientId = entity.PatientId,
                EncounterId = entity.EncounterId,
                PatientName = entity.PatientName,
                Mrn = entity.Mrn,
                ReferralSource = entity.ReferralSource,
                ReferralChannel = entity.ReferralChannel,
                SourceReceivedAt = entity.SourceReceivedAt,
                CaseTitle = entity.CaseTitle,
                CaseSummary = entity.CaseSummary,
                Diagnosis = entity.Diagnosis,
                Priority = entity.Priority,
                Status = entity.Status,
                Assignee = entity.Assignee,
                OwnerRole = entity.OwnerRole,
                NextAction = entity.NextAction,
                NextActionAt = entity.NextActionAt,
                LastUpdate = entity.LastUpdate,
                LastUpdateNote = entity.LastUpdateNote,
                Signal = entity.Signal,
                ReasonInQueue = entity.ReasonInQueue,
                QueueLane = entity.QueueLane,
                BlockerLabel = entity.BlockerLabel,
                DueLabel = entity.DueLabel,
                DueClock = entity.DueClock,
                Contact = entity.Contact,
                PatientDetails = Deserialize<List<ReferralPatientDetailDto>>(entity.PatientDetailsJson) ?? new(),
                LatestNoteAuthor = entity.LatestNoteAuthor,
                Timeline = Deserialize<List<ReferralTimelineItemDto>>(entity.TimelineJson) ?? new(),
                DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                DateUpdated = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
            };
        }

        public static ReferralWorkItem ToEntity(CreateReferralWorkItemDto dto)
        {
            return new ReferralWorkItem
            {
                PatientId = dto.PatientId,
                EncounterId = dto.EncounterId,
                PatientName = dto.PatientName.Trim(),
                Mrn = dto.Mrn.Trim(),
                ReferralSource = dto.ReferralSource.Trim(),
                ReferralChannel = dto.ReferralChannel.Trim(),
                SourceReceivedAt = dto.SourceReceivedAt.Trim(),
                CaseTitle = dto.CaseTitle.Trim(),
                CaseSummary = dto.CaseSummary.Trim(),
                Diagnosis = dto.Diagnosis.Trim(),
                Priority = dto.Priority.Trim(),
                Status = dto.Status.Trim(),
                Assignee = dto.Assignee.Trim(),
                OwnerRole = dto.OwnerRole.Trim(),
                NextAction = dto.NextAction.Trim(),
                NextActionAt = dto.NextActionAt.Trim(),
                LastUpdate = dto.LastUpdate.Trim(),
                LastUpdateNote = dto.LastUpdateNote.Trim(),
                Signal = dto.Signal.Trim(),
                ReasonInQueue = dto.ReasonInQueue.Trim(),
                QueueLane = dto.QueueLane.Trim(),
                BlockerLabel = dto.BlockerLabel.Trim(),
                DueLabel = dto.DueLabel.Trim(),
                DueClock = dto.DueClock.Trim(),
                Contact = dto.Contact.Trim(),
                PatientDetailsJson = Serialize(dto.PatientDetails),
                LatestNoteAuthor = dto.LatestNoteAuthor.Trim(),
                TimelineJson = Serialize(dto.Timeline)
            };
        }

        private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

        private static T? Deserialize<T>(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(value, JsonOptions);
            }
            catch (JsonException)
            {
                return default;
            }
        }
    }
}
