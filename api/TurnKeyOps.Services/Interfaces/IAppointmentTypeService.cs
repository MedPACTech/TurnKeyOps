using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IAppointmentTypeService
    {
        Task<IReadOnlyList<AppointmentTypeDto>> GetAllAsync(bool includeInactive = true, CancellationToken ct = default);
        Task<AppointmentTypeDto> CreateAsync(CreateAppointmentTypeDto dto, CancellationToken ct = default);
        Task<AppointmentTypeDto> UpdateAsync(Guid id, UpdateAppointmentTypeDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
    }
}
