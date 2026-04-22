using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientReferralService
    {
        Task<PatientReferralDto?> GetAsync(Guid patientId, Guid referralId);
        Task<IEnumerable<PatientReferralDto>> GetByPatientAsync(Guid patientId);
        Task<IReadOnlyList<PatientReferralQueueItemDto>> GetQueueAsync(Guid? patientId = null, string? status = null, string? search = null, CancellationToken ct = default);
        Task<PatientReferralDto> RefreshCaseSummaryAsync(Guid patientId, Guid referralId, bool forceRefresh = false, CancellationToken ct = default);
        Task<PatientReferralDto> AddAsync(PatientReferralDto dto);
        Task<PatientReferralDto> UpdateAsync(PatientReferralDto dto);
        Task DeleteAsync(Guid id);
    }
}
