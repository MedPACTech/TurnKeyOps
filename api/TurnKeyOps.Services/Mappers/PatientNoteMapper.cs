using System;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class PatientNoteMapper
    {
        // Map from Entity -> DTO (read back to client)
        public static PatientNoteDto ToDto(PatientNote entity)
        {
            var id = entity.Id != Guid.Empty
                ? entity.Id
                : RepositoryKeyHelper.FromRowKey(entity.RowKey);

            return new PatientNoteDto
            {
                Id = id,
                PatientId = entity.PatientId,
                NoteTypeId = entity.NoteTypeId,
                NoteTypeProfileId = entity.NoteTypeProfileId,
                NoteBody = entity.NoteBody,
                Category = entity.Category,
                Visibility = entity.Visibility,
                DateCreated = entity.DateCreated,
                Tags = entity.Tags
            };
        }

        // Map from Create DTO -> Entity
        public static PatientNote ToEntity(PatientNoteDto dto)
        {
            return new PatientNote
            {
                Id = dto.Id,
                PatientId = dto.PatientId,
                NoteTypeId = dto.NoteTypeId,
                NoteTypeProfileId = dto.NoteTypeProfileId,
                Visibility = dto.Visibility,
                NoteBody = dto.NoteBody,
                Category = dto.Category,
                DateCreated = dto.DateCreated,
                Tags = dto.Tags,
                IsDeleted = false
            };
        }
    }
}
