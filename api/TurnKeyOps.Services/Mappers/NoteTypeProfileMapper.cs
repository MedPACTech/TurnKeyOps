using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class NoteTypeProfileMapper
    {
        public static NoteTypeProfileDto ToDto(NoteTypeProfile entity)
        {
            return new NoteTypeProfileDto
            {
                Id = entity.Id,
                TenantId = entity.TenantId,
                NoteTypeId = entity.NoteTypeId,
                RecordType = entity.RecordType,
                PromptInstructions = entity.PromptInstructions,
                SectionSchemaJson = entity.SectionSchemaJson,
                RequireTelehealthAttestation = entity.RequireTelehealthAttestation,
                RequirePreventiveReview = entity.RequirePreventiveReview,
                IsSystem = entity.IsSystem,
                DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                DateUpdated = entity.DateUpdated.HasValue
                    ? DateTime.SpecifyKind(entity.DateUpdated.Value, DateTimeKind.Utc)
                    : null
            };
        }
    }
}
