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
    public Guid? InvoiceId { get; set; }
    public Guid? QuoteRequestId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? ProjectAddress { get; set; }
    public string? ProjectName { get; set; }
    public EstimateCalculationSnapshotDto? EstimateSnapshot { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string? Crew { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal InvoicedTotal { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal RequiredDepositPercent { get; set; }
    public string? Notes { get; set; }
    public JobPlanningDto Planning { get; set; } = new();
    public List<JobActivityDto> Activity { get; set; } = [];
    public string Version { get; set; } = string.Empty;
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public sealed class JobPlanningDto
{
    public string CustomerConfirmationStatus { get; set; } = "pending";
    public DateTime? CustomerConfirmedAtUtc { get; set; }
    public string? CustomerConfirmationNote { get; set; }
    public string? AccessNotes { get; set; }
    public DateOnly? TargetDate { get; set; }
    public DateOnly? PrepDate { get; set; }
    public DateOnly? PourDate { get; set; }
    public DateOnly? CleanupDate { get; set; }
    public List<JobMaterialDto> Materials { get; set; } = [];
    public Dictionary<string, bool> Checklist { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class JobMaterialDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = "not-started";
    public string? Supplier { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? DeliveryWindow { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Specification { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class JobActivityDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed class JobWorkflowPayloadDto
{
    public JobPlanningDto Planning { get; set; } = new();
    public List<JobActivityDto> Activity { get; set; } = [];
}

public sealed class JobScheduleInputDto
{
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Crew { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? ExpectedVersion { get; set; }
}

public sealed class JobStatusInputDto
{
    public JobStatus Status { get; set; }
    public string? Note { get; set; }
    public string? ExpectedVersion { get; set; }
}

public sealed class JobPlanningInputDto
{
    public JobPlanningDto Planning { get; set; } = new();
    public string? ExpectedVersion { get; set; }
}

public sealed class JobNoteInputDto
{
    public string Note { get; set; } = string.Empty;
    public string? ExpectedVersion { get; set; }
}
