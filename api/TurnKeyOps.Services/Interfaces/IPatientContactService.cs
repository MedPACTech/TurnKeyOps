using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientContactService
    {
        Task<PatientContactDto?> GetAsync(Guid patientId, Guid contactId);
        Task<IEnumerable<PatientContactDto>> GetByPatientAsync(Guid patientId);
        Task<PatientContactDto> AddAsync(PatientContactDto dto);
        Task<PatientContactDto> UpdateAsync(PatientContactDto dto);
        Task DeleteAsync(PatientContactDto dto);
    }
}
