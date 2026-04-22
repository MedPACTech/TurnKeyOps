using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services.Interfaces;
using TurnKeyOps.Services.Mappers;

namespace TurnKeyOps.Services;

public class CalendarEventService : ICalendarEventService
{
    private readonly ICalendarEventRepository _repo;
    private readonly IWeatherService _weatherService;
    private readonly IUserContext _userContext;

    public CalendarEventService(ICalendarEventRepository repo, IWeatherService weatherService, IUserContext userContext)
    {
        _repo = repo;
        _weatherService = weatherService;
        _userContext = userContext;
    }

    private string PartitionKeyForTenant() => RepositoryKeyHelper.ToTenantPartitionKey(_userContext.TenantId);

    public async Task<CalendarEventDto?> GetAsync(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        return entity is null || entity.IsDeleted ? null : CalendarEventMapper.ToDto(entity);
    }

    public async Task<IEnumerable<CalendarEventDto>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        var pk = PartitionKeyForTenant();
        var all = await _repo.GetAllAsync(false, false);
        var events = all
            .Where(e => e.PartitionKey == pk && !e.IsDeleted && e.StartUtc < end && e.EndUtc > start)
            .OrderBy(e => e.StartUtc)
            .Select(CalendarEventMapper.ToDto)
            .ToList();

        // Enrich with weather for events that have a job site
        foreach (var evt in events.Where(e => e.JobSiteId.HasValue))
        {
            try
            {
                var forecasts = await _weatherService.GetForecastForJobSiteAsync(evt.JobSiteId!.Value);
                var dayForecast = forecasts.FirstOrDefault(f =>
                    f.ForecastDate.HasValue && f.ForecastDate.Value.Date == evt.StartUtc.Date);
                if (dayForecast != null) evt.Weather = dayForecast;
            }
            catch { /* weather enrichment is best-effort */ }
        }

        return events;
    }

    public async Task<CalendarEventDto> AddAsync(CalendarEventDto dto)
    {
        dto.Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
        var entity = CalendarEventMapper.ToEntity(dto, PartitionKeyForTenant());
        await _repo.SaveAsync(entity);
        return CalendarEventMapper.ToDto(entity);
    }

    public async Task<CalendarEventDto> UpdateAsync(CalendarEventDto dto)
    {
        var existing = await _repo.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException("Calendar event not found", nameof(dto.Id));
        var entity = CalendarEventMapper.ToEntity(dto, existing.PartitionKey);
        entity.DateCreated = existing.DateCreated;
        await _repo.SaveAsync(entity);
        return CalendarEventMapper.ToDto(entity);
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
