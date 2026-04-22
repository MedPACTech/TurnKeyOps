using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IProcessingCreditUsageRepository : IBaseRepositoryAsync<ProcessingCreditUsage>
    {
        Task<ProcessingCreditUsage?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default);
    }
}
