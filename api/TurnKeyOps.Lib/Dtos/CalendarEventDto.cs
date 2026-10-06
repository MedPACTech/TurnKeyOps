using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Dtos;

public class CalendarEventDto
{
    public string? JobEventType { get; set; }
    public string EventStatus { get; set; } = "scheduled";
    public List<Guid> MembershipIds { get; set; } = [];
    public List<string> ResourceIds { get; set; } = [];
    public bool CustomerVisible { get; set; }
    public Guid Id { get; set; }
    public Guid? LeadId { get; set; }
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
    public string? LocksmithJobType { get; set; }
    public Guid? AssignedTechnicianMembershipId { get; set; }
    public string? Color { get; set; }

    // Weather overlay
    public WeatherForecastDto? Weather { get; set; }

    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}
