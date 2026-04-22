using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientLabsService
    {
        Task<PatientLabsDto?> GetAsync(Guid patientId, Guid labId);
        Task<IEnumerable<PatientLabsDto>> GetByPatientAsync(Guid patientId);
        Task<PatientLabsDto> AddAsync(PatientLabsDto dto);
        Task<PatientLabsDto> UpdateAsync(PatientLabsDto dto);
        Task DeleteAsync(PatientLabsDto dto);
    }
}
