using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public sealed class BobActionRecord : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }
    public bool IsDeleted { get; set; }

    public Guid TenantId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ConversationId { get; set; }
    public string ToolKey { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool ConfirmationRequired { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string InputJson { get; set; } = "{}";
    public string ResultJson { get; set; } = string.Empty;
    public string FailureCode { get; set; } = string.Empty;
    public DateTime ProposedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
