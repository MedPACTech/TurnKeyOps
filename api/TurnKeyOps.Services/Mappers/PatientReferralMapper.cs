using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientReferralMapper
    {
        public static PatientReferralDto ToDto(PatientReferral entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
            {
                id = parsedId;
            }

            return new PatientReferralDto
            {
                Id = id,
                EncounterId = entity.EncounterId,
                CaptureDraftNoteId = entity.CaptureDraftNoteId,
                PatientId = entity.PatientId,
                ProviderId = entity.ProviderId,
                NoteType = entity.NoteType,
                NoteTitle = entity.NoteTitle,
                ReferralBody = entity.ReferralBody,
                Status = entity.Status,
                AssignedToName = entity.AssignedToName,
                OwnerRole = entity.OwnerRole,
                NextAction = entity.NextAction,
                NextActionAt = entity.NextActionAt,
                DueAt = entity.DueAt,
                Priority = entity.Priority,
                ReferralSource = entity.ReferralSource,
                SourceApp = entity.SourceApp,
                ReferralChannel = entity.ReferralChannel,
                Diagnosis = entity.Diagnosis,
                CaseTitle = entity.CaseTitle,
                CaseSummary = entity.CaseSummary,
                ReferralReason = entity.ReferralReason,
                Contact = entity.Contact,
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByFirstName = entity.CreatedByFirstName,
                CreatedByLastName = entity.CreatedByLastName,
                DateSent = entity.DateSent,
                SentBy = entity.SentBy,
                SentTo = entity.SentTo,
                DateCreated = entity.DateCreated,
                DateUpdated = entity.DateUpdated
            };
        }

        public static PatientReferral ToEntity(PatientReferralDto dto)
        {
            return new PatientReferral
            {
                Id = dto.Id,
                EncounterId = dto.EncounterId,
                CaptureDraftNoteId = dto.CaptureDraftNoteId,
                PatientId = dto.PatientId,
                ProviderId = dto.ProviderId,
                NoteType = dto.NoteType,
                NoteTitle = dto.NoteTitle,
                ReferralBody = dto.ReferralBody,
                Status = dto.Status,
                AssignedToName = dto.AssignedToName,
                OwnerRole = dto.OwnerRole,
                NextAction = dto.NextAction,
                NextActionAt = dto.NextActionAt,
                DueAt = dto.DueAt,
                Priority = dto.Priority,
                ReferralSource = dto.ReferralSource,
                SourceApp = dto.SourceApp,
                ReferralChannel = dto.ReferralChannel,
                Diagnosis = dto.Diagnosis,
                CaseTitle = dto.CaseTitle,
                CaseSummary = dto.CaseSummary,
                ReferralReason = dto.ReferralReason,
                Contact = dto.Contact,
                CreatedByUserId = dto.CreatedByUserId,
                CreatedByFirstName = dto.CreatedByFirstName,
                CreatedByLastName = dto.CreatedByLastName,
                DateSent = dto.DateSent,
                SentBy = dto.SentBy,
                SentTo = dto.SentTo,
                DateCreated = dto.DateCreated,
                DateUpdated = dto.DateUpdated,
                IsDeleted = false
            };
        }
    }
}
