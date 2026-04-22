using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class JobSiteService : IJobSiteService
{
    private readonly IJobSiteRepository _repo;
    private readonly IUserContext _userContext;

    public JobSiteService(IJobSiteRepository repo, IUserContext userContext)
    {
        _repo = repo;
        _userContext = userContext;
    }

    private string PartitionKeyForTenant() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<JobSiteDto?> GetAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        return entity is null || entity.IsDeleted ? null : JobSiteMapper.ToDto(entity);
    }

    public async Task<(IEnumerable<JobSiteDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken)
    {
        var pk = PartitionKeyForTenant();
        var offset = int.TryParse(continuationToken, out var parsed) ? parsed : 0;
        var all = (await _repo.GetAllAsync(false, false))
            .Where(x => x.PartitionKey == pk && !x.IsDeleted)
            .OrderByDescending(x => x.DateUpdated)
            .ToList();
        var items = all.Skip(offset).Take(pageSize).ToList();
        var token = offset + items.Count < all.Count ? (offset + items.Count).ToString() : null;
        return (items.Where(x => !x.IsDeleted).Select(JobSiteMapper.ToDto), token);
    }

    public async Task<JobSiteDto> AddAsync(JobSiteDto dto)
    {
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        var entity = JobSiteMapper.ToEntity(dto, PartitionKeyForTenant());
        await _repo.SaveAsync(entity);
        return JobSiteMapper.ToDto(entity);
    }

    public async Task<JobSiteDto> UpdateAsync(JobSiteDto dto)
    {
        var existing = await _repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException("Job site not found", nameof(dto.Id));
        var entity = JobSiteMapper.ToEntity(dto, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        entity.WeatherGridUrl = existing.WeatherGridUrl; // preserve cached weather grid
        await _repo.SaveAsync(entity);
        return JobSiteMapper.ToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity is null) return;
        entity.IsDeleted = true;
        entity.DateUpdated = DateTime.UtcNow;
        await _repo.SaveAsync(entity);
    }
}
