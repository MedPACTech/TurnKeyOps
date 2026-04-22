using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IOperationalAlertService
    {
        Task<OperationalAlertDto> RaiseAsync(RaiseOperationalAlertRequestDto dto, CancellationToken ct = default);
        Task<IReadOnlyList<OperationalAlertDto>> GetRecentAsync(string? status = null, int take = 100, CancellationToken ct = default);
        Task<OperationalAlertDto> AcknowledgeAsync(Guid id, CancellationToken ct = default);
    }
}
