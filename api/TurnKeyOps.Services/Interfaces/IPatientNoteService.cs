using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientNoteService
    {
        Task<PatientNoteDto?> GetAsync(Guid id, Guid patientId);
        Task<IEnumerable<PatientNoteDto>> GetByPatientIdAsync(Guid patientId);
        Task<PatientNoteDto> AddAsync(PatientNoteDto dto);
        Task<PatientNoteDto> UpdateAsync(PatientNoteDto dto);
        Task DeleteAsync(Guid id);
    }
}