using System.Text.Json;

namespace TurnKeyOps.Lib.Dtos;

public sealed class ProposeBobActionDto
{
    public string ToolKey { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public JsonElement Input { get; set; }
}

public sealed class BobActionDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string ToolKey { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool ConfirmationRequired { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public string FailureCode { get; set; } = string.Empty;
    public DateTime ProposedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
}
