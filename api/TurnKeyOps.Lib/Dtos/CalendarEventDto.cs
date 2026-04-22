using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Dtos;

public class CalendarEventDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CalendarEventType EventType { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool AllDay { get; set; }
    public Guid? JobId { get; set; }
    public string? JobName { get; set; }
    public Guid? JobSiteId { get; set; }
    public string? JobSiteName { get; set; }
    public string? Color { get; set; }

    // Weather overlay
    public WeatherForecastDto? Weather { get; set; }

    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}
