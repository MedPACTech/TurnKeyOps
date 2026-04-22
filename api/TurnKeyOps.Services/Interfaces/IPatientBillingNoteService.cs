using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientBillingNoteService
    {
        Task<PatientBillingNoteDto?> GetAsync(Guid patientId, Guid billingNoteId);
        Task<IEnumerable<PatientBillingNoteDto>> GetByPatientAsync(Guid patientId);
        Task<PatientBillingNoteDto> AddAsync(PatientBillingNoteDto dto);
        Task<PatientBillingNoteDto> UpdateAsync(PatientBillingNoteDto dto);
        Task DeleteAsync(Guid id);
    }
}
