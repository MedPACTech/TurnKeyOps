namespace TurnKeyOps.Lib.Dtos;

public class CreateEstimateFromAppointmentRequestDto
{
    public Guid? AppointmentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerCompany { get; set; }
    public string ProjectAddress { get; set; } = string.Empty;
    public string EstimatorName { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string? EstimateNumber { get; set; }
    public StructuredEstimateInputDto StructuredInput { get; set; } = new();
    public List<BobTranscriptEntryDto>? BobTranscript { get; set; }
}
