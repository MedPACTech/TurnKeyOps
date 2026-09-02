using System.Text.Json;

namespace TurnKeyOps.Services.Interfaces;

public enum BobActionRisk
{
    Read,
    Destructive,
    Financial,
    Scheduling,
    CustomerFacing
}

public sealed record BobActionExecutionContext(
    Guid TenantId,
    Guid ActorUserId,
    Guid ConversationId,
    string PartitionKey);

public interface IBobActionProvider
{
    string ToolKey { get; }
    string PermissionKey { get; }
    BobActionRisk Risk { get; }
    Task<object?> ExecuteAsync(BobActionExecutionContext context, JsonElement input, CancellationToken ct = default);
}
