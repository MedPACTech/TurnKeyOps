using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PatientEncounterMapper
    {
        public static PatientEncounterDto ToDto(PatientEncounter entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new PatientEncounterDto
            {
                Id = id,
                PatientId = entity.PatientId,
                Status = entity.Status,
                EncounterBody = entity.EncounterBody,
                CaptureDraftNoteId = entity.CaptureDraftNoteId,
                ProviderId = entity.ProviderId,
                NoteType = entity.NoteType,
                NoteTitle = entity.NoteTitle,
                Data = entity.Data,
                CreatedAt = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(entity.DateUpdated, DateTimeKind.Utc)
            };
        }

        public static PatientEncounter ToEntity(PatientEncounterDto dto, string partitionKey)
        {
            var id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;

            return new PatientEncounter
            {
                Id = id,
                PartitionKey = partitionKey,
                RowKey = id.ToString("D"),
                PatientId = dto.PatientId,
                Status = dto.Status,
                EncounterBody = dto.EncounterBody,
                CaptureDraftNoteId = dto.CaptureDraftNoteId,
                ProviderId = dto.ProviderId,
                NoteType = dto.NoteType,
                NoteTitle = dto.NoteTitle,
                Data = dto.Data,
                DateCreated = dto.CreatedAt == default ? DateTime.UtcNow : dto.CreatedAt.UtcDateTime,
                DateUpdated = DateTime.UtcNow,
                IsDeleted = false
            };
        }
    }
}

