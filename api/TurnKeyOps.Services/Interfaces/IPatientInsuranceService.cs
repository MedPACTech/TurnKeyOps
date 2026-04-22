using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientInsuranceService
    {
        Task<PatientInsuranceDto?> GetAsync(Guid patientId, Guid insuranceId);
        Task<IEnumerable<PatientInsuranceDto>> GetByPatientAsync(Guid patientId);
        Task<PatientInsuranceDto> AddAsync(PatientInsuranceDto dto);
        Task<PatientInsuranceDto> UpdateAsync(PatientInsuranceDto dto);
        Task DeleteAsync(PatientInsuranceDto dto);
    }
}
