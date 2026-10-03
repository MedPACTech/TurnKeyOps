using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface ICalendarEventRepository : IBaseRepositoryAsync<CalendarEvent>
{
    Task<CalendarEvent?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default);
    Task<IReadOnlyCollection<CalendarEvent>> ListAsync(string partitionKey, CancellationToken ct = default);
}
