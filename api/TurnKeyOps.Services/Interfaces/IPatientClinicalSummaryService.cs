using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientClinicalSummaryService
    {
        Task<PatientClinicalSummaryDto> GenerateAsync(Guid patientId, bool forceRefresh = false, CancellationToken ct = default);
    }
}
