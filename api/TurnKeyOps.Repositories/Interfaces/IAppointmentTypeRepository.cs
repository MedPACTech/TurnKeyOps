using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IAppointmentTypeRepository : IBaseRepositoryAsync<AppointmentTypeDefinition>
    {
        Task<IReadOnlyList<AppointmentTypeDefinition>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task<IReadOnlyList<AppointmentTypeDefinition>> GetByTenantAsync(Guid tenantId, bool includeDeleted, CancellationToken ct = default);
        Task<AppointmentTypeDefinition?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default, bool includeDeleted = false);
    }
}
