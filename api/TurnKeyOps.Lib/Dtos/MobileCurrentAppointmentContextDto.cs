namespace TurnKeyOps.Lib.Dtos;

public sealed class MobileCurrentAppointmentContextDto
{
    public Guid AppointmentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCompany { get; set; }
    public string ProjectAddress { get; set; } = string.Empty;
    public DateTime AppointmentDateTime { get; set; }
    public string EstimatorName { get; set; } = string.Empty;
    public Guid? EstimateId { get; set; }
    public string? EstimateNumber { get; set; }
    public string? ProjectName { get; set; }
}
