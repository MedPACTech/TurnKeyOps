namespace TurnKeyOps.Lib.Dtos;

public sealed class QuoteRequestDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string RequestedTimeline { get; set; } = string.Empty;
    public string PreferredTimeline { get; set; } = string.Empty;
    public string Priority { get; set; } = "standard";
    public string Need { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<QuoteRequestAttachmentDto> Attachments { get; set; } = [];
    public string Source { get; set; } = "public-site";
    public string Status { get; set; } = "new";
    public string AssignedTo { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public string IntakeSummary { get; set; } = string.Empty;
    public QuoteRequestQualificationDto Qualification { get; set; } = new();
    public QuoteRequestSubmittedPayloadDto? SubmittedPayload { get; set; }
    public List<QuoteRequestTimelineEventDto> Timeline { get; set; } = [];
    public QuoteRequestSiteVisitScheduleDto? SiteVisitSchedule { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CreateQuoteRequestDto
{
    public Guid? Id { get; set; }
    public string Website { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string RequestedTimeline { get; set; } = string.Empty;
    public string Priority { get; set; } = "standard";
    public string Need { get; set; } = string.Empty;
    public List<QuoteRequestAttachmentDto> Attachments { get; set; } = [];
}

public sealed class QuoteRequestAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public Guid? TenantId { get; set; }
    public string? BlobContainer { get; set; }
    public string? BlobName { get; set; }
    public string? BlobUrl { get; set; }
}

public sealed class QuoteRequestQualificationDto
{
    public List<string> MissingInfoReasonCodes { get; set; } = [];
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }
}

public sealed class QuoteRequestSubmittedPayloadDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string RequestedTimeline { get; set; } = string.Empty;
    public string Priority { get; set; } = "standard";
    public string Need { get; set; } = string.Empty;
    public List<QuoteRequestAttachmentDto> Attachments { get; set; } = [];
}

public sealed class QuoteRequestTimelineEventDto
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Type { get; set; } = "operator-updated";
    public string Actor { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public QuoteRequestSubmittedPayloadDto? Payload { get; set; }
    public string? Note { get; set; }
    public QuoteRequestSiteVisitScheduleDto? SiteVisitSchedule { get; set; }
}

public sealed class QuoteRequestSiteVisitScheduleDto
{
    public string VisitDate { get; set; } = string.Empty;
    public string WindowStart { get; set; } = string.Empty;
    public string WindowEnd { get; set; } = string.Empty;
    public string SiteContact { get; set; } = string.Empty;
    public string SiteContactPhone { get; set; } = string.Empty;
    public string AssignedFieldResource { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public string ScheduledBy { get; set; } = string.Empty;
}
