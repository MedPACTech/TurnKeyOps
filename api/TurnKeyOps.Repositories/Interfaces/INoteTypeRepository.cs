using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface INoteTypeRepository : IBaseRepositoryAsync<NoteType>
    {
        Task<IReadOnlyList<NoteType>> GetSystemDefinitionsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<NoteType>> GetTenantCustomDefinitionsAsync(Guid tenantId, CancellationToken ct = default);
        Task<IReadOnlyList<NoteType>> GetTenantSystemOverridesAsync(Guid tenantId, CancellationToken ct = default);
        Task<NoteType?> GetSystemDefinitionAsync(Guid id, CancellationToken ct = default);
        Task<NoteType?> GetTenantCustomDefinitionAsync(Guid tenantId, Guid id, CancellationToken ct = default);
        Task<NoteType?> GetTenantSystemOverrideAsync(Guid tenantId, Guid systemNoteTypeId, CancellationToken ct = default);
    }
}
