using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface ICalendarEventService
{
    Task<CalendarEventDto?> GetAsync(Guid id);
    Task<IEnumerable<CalendarEventDto>> GetByDateRangeAsync(DateTime start, DateTime end);
    Task<CalendarEventDto> AddAsync(CalendarEventDto dto);
    Task<CalendarEventDto> UpdateAsync(CalendarEventDto dto);
    Task DeleteAsync(Guid id);
}
