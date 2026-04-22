using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientClinicalSummaryCacheRepository : IBaseRepositoryAsync<PatientClinicalSummaryCache>
    {
        Task<PatientClinicalSummaryCache?> GetAsync(Guid tenantId, Guid patientId, CancellationToken ct = default, bool includeDeleted = false);
    }
}
