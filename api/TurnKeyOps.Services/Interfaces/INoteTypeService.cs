using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface INoteTypeService
    {
        Task<IReadOnlyList<NoteTypeDto>> GetAllAsync(CancellationToken ct = default);
        Task<NoteTypeDto> CreateAsync(CreateNoteTypeDto dto, CancellationToken ct = default);
        Task<NoteTypeDto> UpdateAsync(Guid id, UpdateNoteTypeDto dto, CancellationToken ct = default);
        Task<NoteTypeDto> UpdateStatusAsync(Guid id, UpdateNoteTypeStatusDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
