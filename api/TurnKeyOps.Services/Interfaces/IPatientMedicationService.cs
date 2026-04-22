using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientMedicationService
    {
        Task<PatientMedicationDto?> GetAsync(Guid medicationRecordId);
        Task<IEnumerable<PatientMedicationDto>> GetByPatientAsync(Guid patientId);
        Task<IEnumerable<PatientMedicationDto>> GetByProviderAsync(Guid providerId);
        Task<PatientMedicationDto> AddAsync(PatientMedicationDto dto);
        Task<PatientMedicationDto> UpdateAsync(PatientMedicationDto dto);
        Task DeleteAsync(Guid medicationRecordId);
    }
}
