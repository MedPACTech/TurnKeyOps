namespace TurnKeyOps.Lib.Dtos;

// Access metadata only. Operational records remain in their existing repositories.
public sealed class PortalAccessState
{
    public string Version { get; set; } = "";
    public PortalConfiguration Configuration { get; set; } = new();
    public List<PortalGrant> Grants { get; set; } = [];
    public Dictionary<Guid,List<string>> NotificationPreferences { get; set; } = [];
    public List<PortalSession> Sessions { get; set; } = [];
}
public sealed class PortalConfiguration
{
    public bool Enabled { get; set; }
    public string CompanyName { get; set; } = "Your contractor";
    public string LogoPath { get; set; } = "";
    public string ContactInfo { get; set; } = "";
    public string Accent { get; set; } = "teal";
    public bool FinanceEnabled { get; set; }
    public bool FinancePaymentsEnabled { get; set; }
    public bool SelfBookingEnabled { get; set; }
    public bool MessagingEnabled { get; set; } = true;
    public bool UploadEnabled { get; set; } = true;
    public bool CustomerBobEnabled { get; set; } = true;
    public bool ChangeApprovalEnabled { get; set; } = true;
    public bool CompletionAcceptanceEnabled { get; set; } = true;
    public Dictionary<string,string> NotificationTemplates { get; set; } = new() { ["new-message"]="A new message is available in your customer portal.", ["appointment"]="Please review your appointment in your customer portal.", ["proposal"]="Your proposal is ready for review in your customer portal.", ["change"]="A project change needs your review in your customer portal.", ["completion"]="Your completed work is ready for review in your customer portal." };
    public Dictionary<string,string> JobLabels { get; set; } = [];
}
public sealed class PortalGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CustomerId { get; set; }
    public string Scope { get; set; } = "customer";
    public Guid RecordId { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddYears(1);
    public bool Revoked { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedBy { get; set; }
}
public sealed class PortalSession
{
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool Revoked { get; set; }
    public List<string> NotificationChannels { get; set; } = [];
}
public sealed record PortalActor(Guid TenantId, Guid UserId, IReadOnlyList<PortalGrant> Grants, PortalConfiguration Configuration);
public sealed record PortalWork(string Kind, Guid Id, Guid? CustomerId, Guid? SiteId, string Title, string Status, string NextStep, string Version, string? SiteName = null);
public sealed record PortalSlot(Guid Id, DateTime StartUtc, DateTime EndUtc);
public sealed record PortalAppointment(Guid Id, Guid? JobId, Guid? LeadId, DateTime StartUtc, DateTime EndUtc, string Status, string Response, string Version, IReadOnlyList<PortalSlot> Slots);
public sealed record PortalFile(Guid Id, string Name, string ContentType);
public sealed record PortalUpdate(Guid Id, string Text, DateTime AtUtc);
public sealed class PortalCommand
{
    public string ExpectedVersion { get; set; } = "";
    public string Action { get; set; } = "";
    public string Text { get; set; } = "";
    public Guid? ItemId { get; set; }
    public Guid? SlotId { get; set; }
    public string Hash { get; set; } = "";
    public string Signer { get; set; } = "";
    public bool Consent { get; set; }
    public Guid? ChangeEstimateId { get; set; }
}
