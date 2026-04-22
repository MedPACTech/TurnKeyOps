using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface INoteTypeProfileService
    {
        Task<IReadOnlyList<NoteTypeProfileDto>> GetAllAsync(CancellationToken ct = default);
        Task<NoteTypeProfileDto?> GetAsync(Guid id, CancellationToken ct = default);
        Task<NoteTypeProfileDto?> GetByNoteTypeIdAsync(Guid noteTypeId, CancellationToken ct = default);
        Task<NoteTypeProfileDto> CreateAsync(CreateNoteTypeProfileDto dto, CancellationToken ct = default);
        Task<NoteTypeProfileDto> UpdateAsync(Guid id, UpdateNoteTypeProfileDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
