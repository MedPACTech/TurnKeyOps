using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IJobService
{
    Task<JobDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<(IEnumerable<JobDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken, CancellationToken ct = default);
    Task<IEnumerable<JobDto>> GetActiveAsync(CancellationToken ct = default);
    Task<JobDto> AddAsync(JobDto dto, CancellationToken ct = default);
    Task<JobDto> UpdateAsync(JobDto dto, CancellationToken ct = default);
    Task<JobDto> ScheduleAsync(Guid id, JobScheduleInputDto input, CancellationToken ct = default);
    Task<JobDto> UpdateStatusAsync(Guid id, JobStatusInputDto input, CancellationToken ct = default);
    Task<JobDto> UpdatePlanningAsync(Guid id, JobPlanningInputDto input, CancellationToken ct = default);
    Task<JobDto> AddNoteAsync(Guid id, JobNoteInputDto input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
