using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientFamilyMedicalHistoryService
    {
        Task<PatientFamilyMedicalHistoryDto?> GetAsync(Guid patientId, Guid familyMedicalHistoryId);
        Task<IEnumerable<PatientFamilyMedicalHistoryDto>> GetByPatientAsync(Guid patientId);
        Task<PatientFamilyMedicalHistoryDto> AddAsync(PatientFamilyMedicalHistoryDto dto);
        Task<PatientFamilyMedicalHistoryDto> UpdateAsync(PatientFamilyMedicalHistoryDto dto);
        Task DeleteAsync(PatientFamilyMedicalHistoryDto dto);
    }
}
