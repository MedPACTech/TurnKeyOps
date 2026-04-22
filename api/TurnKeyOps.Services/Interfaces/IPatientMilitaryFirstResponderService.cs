using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientMilitaryFirstResponderService
    {
        Task<PatientMilitaryFirstResponderDto?> GetByPatientAsync(Guid patientId);
        Task<PatientMilitaryFirstResponderDto> AddAsync(PatientMilitaryFirstResponderDto dto);
        Task<PatientMilitaryFirstResponderDto> UpdateAsync(PatientMilitaryFirstResponderDto dto);
        Task DeleteAsync(PatientMilitaryFirstResponderDto dto);
    }
}
