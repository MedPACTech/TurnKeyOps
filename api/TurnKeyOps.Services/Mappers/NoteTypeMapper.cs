using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class NoteTypeMapper
    {
        public static NoteTypeDto ToDto(NoteType entity, bool? effectiveIsEnabled = null, bool? effectiveIsDefault = null)
        {
            return new NoteTypeDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Code = entity.Code,
                Description = entity.Description,
                HasParentNote = entity.HasParentNote,
                IsSystem = entity.IsSystem,
                IsEnabled = effectiveIsEnabled ?? entity.IsEnabled,
                IsDefault = effectiveIsDefault ?? entity.IsDefault ?? false,
                SortOrder = entity.SortOrder,
                DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                DateUpdated = entity.DateUpdated.HasValue
                    ? DateTime.SpecifyKind(entity.DateUpdated.Value, DateTimeKind.Utc)
                    : null
            };
        }
    }
}
