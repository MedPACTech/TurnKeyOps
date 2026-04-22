using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IActivityLogRepository : IBaseRepositoryAsync<ActivityLog>
    {
        Task<ActivityLog?> GetByContextAsync(Guid tenantId, Guid userId, DateTime entryDate, CancellationToken ct = default);
        Task UpsertAsync(ActivityLog entity, CancellationToken ct = default);
    }
}
