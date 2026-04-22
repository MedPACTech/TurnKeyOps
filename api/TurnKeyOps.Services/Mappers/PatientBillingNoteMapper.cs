using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientBillingNoteMapper
    {
        public static PatientBillingNoteDto ToDto(PatientBillingNote entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
            {
                id = parsedId;
            }

            return new PatientBillingNoteDto
            {
                Id = id,
                EncounterId = entity.EncounterId,
                CaptureDraftNoteId = entity.CaptureDraftNoteId,
                PatientId = entity.PatientId,
                ProviderId = entity.ProviderId,
                NoteType = entity.NoteType,
                NoteTitle = entity.NoteTitle,
                BillingBody = entity.BillingBody,
                DateSigned = entity.DateSigned,
                SignedBy = entity.SignedBy,
                DateCreated = entity.DateCreated,
                DateUpdated = entity.DateUpdated
            };
        }

        public static PatientBillingNote ToEntity(PatientBillingNoteDto dto)
        {
            return new PatientBillingNote
            {
                Id = dto.Id,
                EncounterId = dto.EncounterId,
                CaptureDraftNoteId = dto.CaptureDraftNoteId,
                PatientId = dto.PatientId,
                ProviderId = dto.ProviderId,
                NoteType = dto.NoteType,
                NoteTitle = dto.NoteTitle,
                BillingBody = dto.BillingBody,
                DateSigned = dto.DateSigned,
                SignedBy = dto.SignedBy,
                DateCreated = dto.DateCreated,
                DateUpdated = dto.DateUpdated,
                IsDeleted = false
            };
        }
    }
}
