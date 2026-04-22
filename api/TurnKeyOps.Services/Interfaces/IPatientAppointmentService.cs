using MedInsights.Lib.Dtos;
using static MedInsights.Services.PatientAppointmentService;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientAppointmentService
    {
        Task<PatientAppointmentDto?> GetAsync(Guid id);
        Task<PatientAppointmentDto> AddAsync(PatientAppointmentDto dto);
        Task<PatientAppointmentDto> UpdateAsync(PatientAppointmentDto dto);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<PatientAppointmentDto>> SearchAsync(AppointmentSearch filter, CancellationToken cancellationToken = default);
    }
}
