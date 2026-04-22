using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IActivityLogService
    {
        Task<ActivityLogDto?> GetAsync(Guid tenantId,Guid userId, DateTime entryDate, CancellationToken ct = default);
        Task<ActivityLogDto> UpsertAsync(ActivityLogUpsertDto dto, CancellationToken ct = default);
        Task<IEnumerable<ActivityLogItemDto>> GetEntryForUserByDateAsync(DateTime entryDate, CancellationToken ct = default);
        Task<IReadOnlyList<ActivityReadDto>> GetEntriesForMonthAsync(DateTime month, CancellationToken ct = default);
    }
}
