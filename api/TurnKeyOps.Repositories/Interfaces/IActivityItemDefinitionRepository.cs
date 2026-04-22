using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IActivityItemDefinitionRepository : IBaseRepositoryAsync<ActivityItemDefinition>
    {
        Task<IReadOnlyList<ActivityItemDefinition>> GetActiveDefinitionsAsync(Guid tenantId, CancellationToken ct = default);
    }
}
