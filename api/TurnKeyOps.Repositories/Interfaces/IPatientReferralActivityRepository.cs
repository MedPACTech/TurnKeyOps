using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IPatientReferralActivityRepository : IBaseRepositoryAsync<PatientReferralActivity>
    {
        Task<IReadOnlyList<PatientReferralActivity>> GetByReferralAsync(Guid tenantId, Guid patientReferralId, CancellationToken ct = default);
    }
}
