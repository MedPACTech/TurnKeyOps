using MedInsights.Lib.Dtos;
using MedInsights.Lib.Enums;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientVitalsService
    {
        Task<PatientVitalsDto?> GetAsync(Guid patientId, Guid vitalsId, VitalsUnitSystem? unitSystem = null);
        Task<IEnumerable<PatientVitalsDto>> GetByPatientAsync(Guid patientId, VitalsUnitSystem? unitSystem = null);
        Task<PatientVitalsDto> AddAsync(PatientVitalsDto dto);
        Task<PatientVitalsDto> UpdateAsync(PatientVitalsDto dto);
        Task DeleteAsync(PatientVitalsDto dto);
    }
}
