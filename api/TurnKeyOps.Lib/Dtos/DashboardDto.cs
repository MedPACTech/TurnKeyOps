namespace TurnKeyOps.Lib.Dtos;

/// <summary>Aggregated dashboard data for the contractor home screen.</summary>
public class DashboardDto
{
    public int ActiveJobs { get; set; }
    public int PendingEstimates { get; set; }
    public int OverdueInvoices { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal OutstandingBalance { get; set; }
    public List<CalendarEventDto> UpcomingEvents { get; set; } = new();
    public List<WeatherForecastDto> WeatherForecasts { get; set; } = new();
}
