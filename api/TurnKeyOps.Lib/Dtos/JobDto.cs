using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Dtos;

public class JobDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TradeType TradeType { get; set; }
    public JobStatus Status { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? JobSiteId { get; set; }
    public string? JobSiteName { get; set; }
    public Guid? EstimateId { get; set; }
    public string? EstimateNumber { get; set; }
    public string? ProjectAddress { get; set; }
    public string? ProjectName { get; set; }
    public EstimateCalculationSnapshotDto? EstimateSnapshot { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal InvoicedTotal { get; set; }
    public decimal PaidTotal { get; set; }
    public string? Notes { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}
