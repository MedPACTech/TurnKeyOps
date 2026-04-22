using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientAllergyService
    {
        Task<PatientAllergyDto?> GetAsync(Guid patientId, Guid allergyId);
        Task<IEnumerable<PatientAllergyDto>> GetByPatientAsync(Guid patientId);
        Task<PatientAllergyDto> AddAsync(PatientAllergyDto dto);
        Task<PatientAllergyDto> UpdateAsync(PatientAllergyDto dto);
        Task DeleteAsync(PatientAllergyDto dto);
    }
}
