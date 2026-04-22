using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IAuditService
    {
        Task<AuditEventDto> RecordAsync(RecordAuditEventRequestDto dto, CancellationToken ct = default);
        Task<IReadOnlyList<AuditEventDto>> GetRecentAsync(int take = 100, CancellationToken ct = default);
    }
}
