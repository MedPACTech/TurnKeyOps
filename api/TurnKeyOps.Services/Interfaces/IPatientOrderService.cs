using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPatientOrderService
    {
        Task<PatientOrderDto?> GetAsync(Guid orderId);
        Task<IEnumerable<PatientOrderDto>> GetByPatientAsync(Guid patientId);
        Task<IEnumerable<PatientOrderDto>> GetByProviderAsync(Guid providerId);
        Task<PatientOrderDto> AddAsync(PatientOrderDto dto);
        Task<PatientOrderDto> UpdateAsync(PatientOrderDto dto);
        Task DeleteAsync(Guid orderId);
    }
}
