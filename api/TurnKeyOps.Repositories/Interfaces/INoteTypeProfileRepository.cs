using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface INoteTypeProfileRepository : IBaseRepositoryAsync<NoteTypeProfile>
    {
        Task<IReadOnlyList<NoteTypeProfile>> GetSystemProfilesAsync(CancellationToken ct = default);
        Task<IReadOnlyList<NoteTypeProfile>> GetTenantProfilesAsync(Guid tenantId, CancellationToken ct = default);
        Task<NoteTypeProfile?> GetSystemProfileAsync(Guid id, CancellationToken ct = default);
        Task<NoteTypeProfile?> GetTenantProfileAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<NoteTypeProfile?> GetSystemProfileByNoteTypeIdAsync(Guid noteTypeId, CancellationToken ct = default);
        Task<NoteTypeProfile?> GetTenantProfileByNoteTypeIdAsync(Guid tenantId, Guid noteTypeId, CancellationToken ct = default);
    }
}
