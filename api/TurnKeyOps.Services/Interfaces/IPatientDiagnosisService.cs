using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientDiagnosisService
    {
        Task<PatientDiagnosisDto?> GetAsync(Guid patientId, Guid diagnosisId);
        Task<IEnumerable<PatientDiagnosisDto>> GetByPatientAsync(Guid patientId);
        Task<PatientDiagnosisDto> AddAsync(PatientDiagnosisDto dto);
        Task<PatientDiagnosisDto> UpdateAsync(PatientDiagnosisDto dto);
        Task DeleteAsync(PatientDiagnosisDto dto);
    }
}
