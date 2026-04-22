using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
public interface IPatientContextService 
    {
        // Return the currently active patient context (newest by DateActivated)
        Task<PatientContextDto?> GetActiveAsync();

        // Return all historical patient contexts (max 10 records, oldest purged)
        Task<IEnumerable<PatientContextDto>> GetHistoryAsync();

        // Explicitly activate a patient
        Task<PatientContextDto> ActivateAsync(PatientDto patient);
    }
}