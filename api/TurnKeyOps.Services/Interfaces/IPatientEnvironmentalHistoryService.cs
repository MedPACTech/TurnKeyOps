using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientEnvironmentalHistoryService
    {
        Task<PatientEnvironmentalHistoryDto?> GetByPatientAsync(Guid patientId);
        Task<PatientEnvironmentalHistoryDto> AddAsync(PatientEnvironmentalHistoryDto dto);
        Task<PatientEnvironmentalHistoryDto> UpdateAsync(PatientEnvironmentalHistoryDto dto);
        Task DeleteAsync(PatientEnvironmentalHistoryDto dto);
    }
}
