namespace TurnKeyOps.Lib.Dtos;

public static class LeadStages
{
    public static readonly string[] All = ["NEW", "NEEDS_RESPONSE", "QUALIFYING", "QUALIFIED", "DISCOVERY", "READY_TO_ESTIMATE", "ESTIMATING", "PROPOSAL", "FOLLOW_UP", "WON", "LOST"];
    public static bool IsClosed(string stage) => stage is "WON" or "LOST";
    public static string FromIntake(string status) => status switch
    {
        "in-review" => "QUALIFYING", "needs-info" => "NEEDS_RESPONSE", "qualified" => "QUALIFIED",
        "contacted" => "QUALIFYING", "inspection-scheduled" => "DISCOVERY", "estimate-drafted" => "ESTIMATING",
        "estimate-sent" => "PROPOSAL", "won" => "WON", "closed" => "LOST", _ => "NEW"
    };
    public static string NextAction(string stage) => stage switch
    {
        "NEW" or "NEEDS_RESPONSE" => "Respond to the customer", "QUALIFYING" => "Complete qualification",
        "QUALIFIED" => "Arrange discovery", "DISCOVERY" => "Record discovery findings", "READY_TO_ESTIMATE" => "Create estimate draft",
        "ESTIMATING" => "Prepare the estimate", "PROPOSAL" => "Confirm the customer's decision", "FOLLOW_UP" => "Follow up with the customer",
        "WON" => "Hand off to the job", "LOST" => "Review outcome or reopen", _ => "Review lead"
    };
}

public class LeadInput
{
    public string Title { get; set; } = "";
    public Guid? CustomerId { get; set; }
    public string ContactName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string SiteAddress { get; set; } = "";
    public string RequestedWork { get; set; } = "";
    public string TradeProfile { get; set; } = "general";
    public string Service { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public string Source { get; set; } = "Manual";
    public Guid? OwnerMembershipId { get; set; }
    public Guid? OwnerProfileId { get; set; }
    public decimal? EstimatedValue { get; set; }
    public Guid? ReferralContactId { get; set; }
    public string ReferralName { get; set; } = "";
    public string NextAction { get; set; } = "";
    public DateTime? FollowUpAtUtc { get; set; }
    public Dictionary<string, string> Qualification { get; set; } = [];
    public Dictionary<string, string> Attribution { get; set; } = [];
}

public sealed class CreateLeadDto : LeadInput
{
    public Guid Id { get; set; }
    // Explicitly creating a new customer never merges a suggested match.
    public bool CreateCustomer { get; set; }
}
public sealed class UpdateLeadDto : LeadInput { public string ExpectedVersion { get; set; } = ""; }
public sealed class LeadStageDto
{
    public string Stage { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ExpectedVersion { get; set; } = "";
}
public sealed class LeadNoteDto
{
    public string Type { get; set; } = "note";
    public string Text { get; set; } = "";
    public string ExpectedVersion { get; set; } = "";
}
public sealed class LeadActivityDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Text { get; set; } = "";
    public Guid? RelatedId { get; set; }
}
public sealed class LeadDto : LeadInput
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? IntakeRequestId { get; set; }
    public string Stage { get; set; } = "NEW";
    public string StageLabel { get; set; } = "New";
    public string OwnerName { get; set; } = "Unassigned";
    public string CloseReason { get; set; } = "";
    public Guid? EstimateId { get; set; }
    public Guid? JobId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? FirstResponseAtUtc { get; set; }
    public DateTime? WonAtUtc { get; set; }
    public DateTime? LostAtUtc { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public string Version { get; set; } = "";
    public List<LeadActivityDto> Activity { get; set; } = [];
    public List<LeadFieldDto> Fields { get; set; } = [];
    public List<string> MissingRequired { get; set; } = [];
    public string BobSummary { get; set; } = "";
}
public sealed record LeadFieldDto(string Key, string Label, bool Required, string Value);
public sealed record LeadDuplicateDto(Guid Id, string Kind, string Name, string Match);
public sealed class LeadConfigurationDto
{
    public string[] TradeProfiles { get; set; } = ["general", "concrete", "framing", "land-clearing", "doors-locks"];
    public string DefaultTradeProfile { get; set; } = "general";
    public Dictionary<string, string> StageLabels { get; set; } = [];
    public Dictionary<string, Dictionary<string, string>> RequiredFields { get; set; } = [];
    public string[] WonReasons { get; set; } = ["Accepted proposal", "Repeat customer", "Other"];
    public string[] LostReasons { get; set; } = ["Price", "Timing", "No response", "Not a fit", "Other"];
    public bool ReferralRequired { get; set; }
    public string AssignmentMode { get; set; } = "manual";
    public List<LeadAssignmentRuleDto> AssignmentRules { get; set; } = [];
    public Dictionary<string, string> AiActions { get; set; } = [];
}
public sealed class LeadAssignmentRuleDto
{
    public Guid MembershipId { get; set; }
    public string Trade { get; set; } = "";
    public string Service { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public string Source { get; set; } = "";
    public string Territory { get; set; } = "";
    public int Priority { get; set; }
}
public sealed record LeadMemberDto(Guid Id, string Name);
public sealed record LeadWorkspaceDto(IReadOnlyList<LeadDto> Leads, LeadConfigurationDto Configuration,
    IReadOnlyList<LeadMemberDto> Members, bool CanWrite, bool CanConfigure, IReadOnlyList<LeadMemberDto>? Associates = null);
public sealed class LeadScheduleDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string ExpectedVersion { get; set; } = "";
}
public sealed class LeadCommunicationDto
{
    public string Channel { get; set; } = "email";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string ExpectedVersion { get; set; } = "";
}
