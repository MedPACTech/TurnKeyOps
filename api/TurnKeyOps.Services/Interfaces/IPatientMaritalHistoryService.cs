using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientMaritalHistoryService
    {
        Task<PatientMaritalHistoryDto?> GetByPatientAsync(Guid patientId);
        Task<PatientMaritalHistoryDto> AddAsync(PatientMaritalHistoryDto dto);
        Task<PatientMaritalHistoryDto> UpdateAsync(PatientMaritalHistoryDto dto);
        Task DeleteAsync(PatientMaritalHistoryDto dto);
    }
}
