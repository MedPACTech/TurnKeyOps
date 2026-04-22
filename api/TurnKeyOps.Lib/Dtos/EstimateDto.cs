using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Dtos;

public class EstimateDto
{
    public Guid Id { get; set; }
    public string EstimateNumber { get; set; } = string.Empty;
    public EstimateStatus Status { get; set; }
    public TradeType TradeType { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCompany { get; set; }
    public Guid? JobId { get; set; }
    public string? JobName { get; set; }
    public Guid? JobSiteId { get; set; }
    public Guid? ConvertedJobId { get; set; }
    public string? ProjectAddress { get; set; }
    public string? EstimatorName { get; set; }
    public string? ProjectName { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    // Concrete
    public double? TotalSqft { get; set; }
    public double? DepthInches { get; set; }
    public double? CubicYards { get; set; }
    public int? NumberOfPours { get; set; }

    // Framing
    public double? WallLinearFeet { get; set; }
    public int? StudCount { get; set; }

    public List<EstimateLineItemDto> LineItems { get; set; } = new();

    public DateTime? SentDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? RevisedDate { get; set; }
    public DateTime? AwardedDate { get; set; }
    public DateTime? RejectedDate { get; set; }
    public DateTime? ConvertedToJobDate { get; set; }

    // Signature
    public string? SignatureDataUrl { get; set; }
    public string? SignedByName { get; set; }
    public DateTime? SignedDate { get; set; }

    public string? Notes { get; set; }
    public StructuredEstimateInputDto? StructuredInput { get; set; }
    public EstimateCalculationSnapshotDto? CalculationSnapshot { get; set; }
    public List<BobTranscriptEntryDto> BobTranscript { get; set; } = new();
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}
