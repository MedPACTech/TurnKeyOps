using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientReferralActivityService
    {
        Task<IReadOnlyList<PatientReferralActivityDto>> GetByReferralAsync(Guid patientReferralId, CancellationToken ct = default);
        Task<PatientReferralActivityDto> AddNoteAsync(Guid patientReferralId, CreatePatientReferralActivityNoteDto dto, CancellationToken ct = default);
        Task<PatientReferralActivityDto> AppendAsync(CreatePatientReferralActivityDto dto, CancellationToken ct = default);
    }
}
