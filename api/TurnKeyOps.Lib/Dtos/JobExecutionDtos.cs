using TurnKeyOps.Lib.Enums;
namespace TurnKeyOps.Lib.Dtos;

// Universal execution data lives in the existing Job workflow payload. Trade fields are schema-bound.
public sealed class JobExecutionDto
{
    [System.Text.Json.Serialization.JsonIgnore] public List<string> SupplyBlockers { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public Dictionary<Guid,string> SupplyRequirementStatuses { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public HashSet<Guid> SupplyRequirementIds { get; set; } = [];
    public int SchemaVersion { get; set; } = 1;
    public string Origin { get; set; } = "Manual";
    public string? SourceSystem { get; set; }
    public string? ExternalId { get; set; }
    public string TradeProfile { get; set; } = "general";
    public string ServiceType { get; set; } = "";
    public string Priority { get; set; } = "normal";
    public Guid? OwnerMembershipId { get; set; }
    public string SoldScope { get; set; } = "";
    public Dictionary<string,string> TradeData { get; set; } = [];
    public JobProfileDto Profile { get; set; } = new();
    public List<JobTaskDto> Tasks { get; set; } = [];
    public List<JobRequirementDto> Requirements { get; set; } = [];
    public List<JobIssueDto> Issues { get; set; } = [];
    public List<JobChangeDto> Changes { get; set; } = [];
    public List<JobEvidenceDto> Evidence { get; set; } = [];
    public List<JobAcceptanceDto> Acceptances { get; set; } = [];
    public DateTime? WarrantyEndsAtUtc { get; set; }
    public string? BillingReference { get; set; }
    public int CompletionRevision { get; set; } = 1;
}
public sealed class JobTaskDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Stage { get; set; } = "complete";
    public bool Required { get; set; } = true;
    public bool EvidenceRequired { get; set; }
    public Guid? OwnerMembershipId { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public List<Guid> DependsOn { get; set; } = [];
    public string TemplateSource { get; set; } = "";
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletedBy { get; set; }
    public string Notes { get; set; } = "";
    public List<Guid> EvidenceIds { get; set; } = [];
}
public sealed class JobRequirementDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "material";
    public string Description { get; set; } = "";
    public string? CatalogItemId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public string Unit { get; set; } = "each";
    public string Status { get; set; } = "Needed";
    public DateTime? RequiredAtUtc { get; set; }
    public string? ResourceId { get; set; }
    public string? Vendor { get; set; }
    public string? Source { get; set; }
    public string Notes { get; set; } = "";
}
public sealed class JobIssueDto
{
    public Guid? PortalUserId { get; set; }
    public Guid? OriginalJobId { get; set; }
    public string? ServiceKind { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "other";
    public string Severity { get; set; } = "blocker";
    public string Description { get; set; } = "";
    public Guid? OwnerMembershipId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string Resolution { get; set; } = "";
    public List<Guid> EvidenceIds { get; set; } = [];
}
public sealed class JobChangeDto
{
    public bool CustomerVisible { get; set; }
    public string? CustomerDecisionHash { get; set; }
    public Guid? PortalUserId { get; set; }
    public DateTime? CustomerDecidedAtUtc { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Origin { get; set; } = "field";
    public string RequestedBy { get; set; } = "";
    public string Description { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ScopeImpact { get; set; } = "";
    public string ScheduleImpact { get; set; } = "";
    public bool PricingImpact { get; set; }
    public Guid? AcceptedEstimateId { get; set; }
    public int? AcceptedRevision { get; set; }
    public string? AcceptedDocumentHash { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string CustomerApproval { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public List<Guid> EvidenceIds { get; set; } = [];
}
public sealed class JobEvidenceDto
{
    public bool CustomerVisible { get; set; }
    public string Purpose { get; set; } = "field";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string BlobName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string Actor { get; set; } = "";
}
public sealed class JobAcceptanceDto
{
    public string CompletionHash { get; set; } = "";
    public string AcceptedState { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CompletionRevision { get; set; }
    public string Signer { get; set; } = "";
    public string Statement { get; set; } = "";
    public string Exceptions { get; set; } = "";
    public string Signature { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public DateTime AcceptedAtUtc { get; set; }
}
public sealed class JobProfileDto
{
    public string Version { get; set; } = "1";
    public Dictionary<string,string> Fields { get; set; } = [];
    public List<string> RequiredFields { get; set; } = [];
    public List<string> RequiredSkills { get; set; } = [];
    public List<JobTaskDto> Tasks { get; set; } = [];
    public int CompletionPhotos { get; set; }
    public bool CustomerAcceptanceRequired { get; set; } = true;
    public bool AllowQualifiedAcceptance { get; set; }
    public Dictionary<string,string> StateLabels { get; set; } = [];
}
public sealed class JobConfigurationDto
{
    public Dictionary<string,string> AiActions { get; set; } = [];
    public bool AllowManual { get; set; } = true;
    public string[] EnabledTrades { get; set; } = ["general","concrete","framing","land-clearing","doors-locks"];
    public Dictionary<string,JobProfileDto> Profiles { get; set; } = [];
    public Dictionary<Guid,List<string>> MemberSkills { get; set; } = [];
    public Dictionary<string,string> NotificationTemplates { get; set; } = [];
    public Dictionary<Guid,List<string>> CustomerChannels { get; set; } = [];
}
public sealed class JobCommandDto
{
    public string ExpectedVersion { get; set; } = "";
    public string Action { get; set; } = "";
    public string Text { get; set; } = "";
    public string State { get; set; } = "";
    public Guid? ItemId { get; set; }
    public Guid? OwnerMembershipId { get; set; }
    public JobTaskDto? Task { get; set; }
    public JobRequirementDto? Requirement { get; set; }
    public JobIssueDto? Issue { get; set; }
    public JobChangeDto? Change { get; set; }
    public JobAcceptanceDto? Acceptance { get; set; }
    public Dictionary<string,string>? TradeData { get; set; }
    public List<Guid> EvidenceIds { get; set; } = [];
}
public sealed class JobCreateDto
{
    public string Name { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Guid JobSiteId { get; set; }
    public string TradeProfile { get; set; } = "general";
    public string ServiceType { get; set; } = "";
    public string Origin { get; set; } = "Manual";
    public string? SourceSystem { get; set; }
    public string? ExternalId { get; set; }
    public string Scope { get; set; } = "";
}
public sealed class JobEventInputDto
{
    public string ExpectedVersion { get; set; } = "";
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Type { get; set; } = "work";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public List<Guid> MembershipIds { get; set; } = [];
    public List<string> ResourceIds { get; set; } = [];
    public bool CustomerVisible { get; set; }
    public string Status { get; set; } = "scheduled";
    public string Notes { get; set; } = "";
}
public sealed record JobWorkspaceDto(JobDto Job,string State,string StateLabel,string NextAction,
    IReadOnlyList<string> ScheduleBlockers,IReadOnlyList<string> StartBlockers,IReadOnlyList<string> CompletionBlockers,
    IReadOnlyList<string> Transitions,IReadOnlyList<CalendarEventDto> Events,bool CanWrite);
