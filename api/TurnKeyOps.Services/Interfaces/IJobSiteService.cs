using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IJobSiteService
{
    Task<JobSiteDto?> GetAsync(Guid id);
    Task<(IEnumerable<JobSiteDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken);
    Task<JobSiteDto> AddAsync(JobSiteDto dto);
    Task<JobSiteDto> UpdateAsync(JobSiteDto dto);
    Task DeleteAsync(Guid id);
}
