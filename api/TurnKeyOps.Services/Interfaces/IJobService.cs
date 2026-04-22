using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IJobService
{
    Task<JobDto?> GetAsync(Guid id);
    Task<(IEnumerable<JobDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken);
    Task<IEnumerable<JobDto>> GetActiveAsync();
    Task<JobDto> AddAsync(JobDto dto);
    Task<JobDto> UpdateAsync(JobDto dto);
    Task DeleteAsync(Guid id);
}
